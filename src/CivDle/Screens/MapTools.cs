using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Nástroje, kterými hráč zasahuje do mapy: stavba, sázení, malování zón a přesun
/// budovy. Drží celý jejich stav i „ducha" pod kurzorem a hlavně <b>vynucuje
/// jediný aktivní nástroj</b> — dřív se to hlídalo ručně na šesti místech
/// (`_plantMode = false; _zoneMode = false; _selectedBuilding = -1;`), což je
/// přesně místo, kde vznikají chyby typu „maluju zónu a zároveň stavím".
///
/// <para>Odděleno z <c>GameplayScreen</c>, který dělal vstup, režimy, HUD i kreslení
/// v jednom souboru (SRP z CLAUDE.md). Tahle třída nezná Myru ani kreslení —
/// jen počítá, co se má stát; herní obrazovka si z ní čte, co vykreslit.</para>
/// </summary>
public sealed class MapTools
{
    /// <summary>Tolerance tažení pravým tlačítkem, aby se pan kamery nepletl s klikem.</summary>
    private const float RightClickDragTolerance = 6f;

    /// <summary>
    /// O kolik dlaždic musí kurzor ujet, než se z kliknutí stane tažení. Bez
    /// tolerance by se každý klik roztřesenou rukou počítal jako hromadná stavba.
    /// </summary>
    private const int DragThresholdTiles = 1;

    private readonly Simulation _simulation;
    private readonly Camera2D _camera;
    private readonly InputManager _input;
    private readonly CivDle.Core.Content.GameContent _content;
    private readonly BulkBuilder _bulk;

    /// <summary>Plán hromadné stavby. Drží se mezi snímky, ať se při tažení nealokuje.</summary>
    private readonly List<BulkSlot> _plan = new();

    private float _rightDragDistance;
    private bool _dragging;
    private int _dragStartX, _dragStartY;

    public MapTools(Simulation simulation, Camera2D camera, InputManager input, CivDle.Core.Content.GameContent content)
    {
        _simulation = simulation;
        _camera = camera;
        _input = input;
        _content = content;
        _bulk = new BulkBuilder(simulation, content);
    }

    /// <summary>Vybraná budova ke stavbě, nebo −1.</summary>
    public int SelectedBuilding { get; private set; } = -1;

    /// <summary>Běží režim sázení?</summary>
    public bool PlantMode { get; private set; }

    /// <summary>Běží režim malování zón?</summary>
    public bool ZoneMode { get; private set; }

    /// <summary>Typ zóny, který se právě maluje.</summary>
    public int ZonePaintTypeIndex { get; private set; }

    /// <summary>Index přesouvané budovy, nebo −1.</summary>
    public int MovingBuildingIndex { get; private set; } = -1;

    /// <summary>Index zvoleného terraformačního zásahu, nebo −1.</summary>
    public int TerraformIndex { get; private set; } = -1;

    /// <summary>Slučování bloků 2×2 v jednu velkou budovu.</summary>
    public bool MergeMode { get; private set; }

    /// <summary>Ruční kladení silnic.</summary>
    public bool RoadMode { get; private set; }

    /// <summary>Bourání silnic.</summary>
    public bool RoadEraseMode { get; private set; }

    /// <summary>Je aktivní jakýkoli nástroj? (Jinak klik na mapu znamená těžbu.)</summary>
    public bool AnyActive =>
        SelectedBuilding >= 0 || PlantMode || ZoneMode || MovingBuildingIndex >= 0 || TerraformIndex >= 0
        || MergeMode || RoadMode || RoadEraseMode;

    // ----- duch pod kurzorem (čte render) -----

    public bool GhostVisible { get; private set; }
    public int GhostX { get; private set; }
    public int GhostY { get; private set; }
    public PlacementResult GhostResult { get; private set; }

    /// <summary>
    /// Plán hromadné stavby — kam všude by šla budova, kdyby hráč pustil tlačítko.
    /// Prázdný, když se staví po jednom. Render z něj kreslí duchy.
    /// </summary>
    public IReadOnlyList<BulkSlot> BulkPlan => _plan;

    /// <summary>Kolik kusů plán opravdu postaví (pro popisek u kurzoru).</summary>
    public int BulkBuildable { get; private set; }

    /// <summary>Nabídka násobičů z dat (×1, ×5, ×25…).</summary>
    public IReadOnlyList<int> BatchSizes => _content.Gameplay.BulkBuild.Batches;

    /// <summary>
    /// Kolik kusů položí jedno kliknutí. Přepíná se v liště a drží se mezi
    /// budovami — hráč, který staví po pětadvaceti, to obvykle chce dělat dál.
    /// </summary>
    public int BatchSize { get; private set; } = 1;

    /// <summary>Přepne násobič hromadné stavby.</summary>
    public void SetBatchSize(int size) => BatchSize = Math.Max(1, size);

    /// <summary>Posune násobič o krok v nabídce dokola (klávesa i gamepad).</summary>
    public void CycleBatchSize()
    {
        var sizes = BatchSizes;
        int index = 0;
        for (int i = 0; i < sizes.Count; i++)
        {
            if (sizes[i] == BatchSize)
            {
                index = i;
                break;
            }
        }

        BatchSize = sizes[(index + 1) % sizes.Count];
    }

    /// <summary>Kolik kusů vybraného typu si hráč zrovna může dovolit.</summary>
    public int AffordableCount =>
        SelectedBuilding >= 0 ? _bulk.Affordable(SelectedBuilding) : 0;

    public bool MoveGhostActive { get; private set; }
    public int MoveGhostX { get; private set; }
    public int MoveGhostY { get; private set; }
    public PlacementResult MoveGhostResult { get; private set; }

    public bool PlantGhostActive { get; private set; }
    public int PlantGhostX { get; private set; }
    public int PlantGhostY { get; private set; }
    public PlacementResult PlantGhostResult { get; private set; }

    public bool ZonePreviewActive { get; private set; }

    /// <summary>Náhled bloku, který se sloučí (levý horní roh čtverce 2×2).</summary>
    public bool MergeGhostActive { get; private set; }
    public int MergeGhostX { get; private set; }
    public int MergeGhostY { get; private set; }
    public PlacementResult MergeGhostResult { get; private set; }

    /// <summary>Náhled dlaždice silnice pod kurzorem.</summary>
    public bool RoadGhostActive { get; private set; }
    public int RoadGhostX { get; private set; }
    public int RoadGhostY { get; private set; }
    public bool RoadGhostErasing { get; private set; }
    public PlacementResult RoadGhostResult { get; private set; }

    /// <summary>
    /// Trasa, kterou hráč zrovna táhne (v dlaždicích). Prázdná, když netáhne.
    ///
    /// <para>Render si ji jen přečte a nakreslí — díky tomu je vidět <b>celá</b>
    /// budoucí ulice ještě před puštěním tlačítka.</para>
    /// </summary>
    public IReadOnlyList<(int X, int Y)> RoadGhostPath => _roadPath;

    private readonly List<(int X, int Y)> _roadPath = new();
    private bool _roadDragging;
    private int _roadAnchorX;
    private int _roadAnchorY;
    public Rectangle ZonePreview { get; private set; }

    public bool TerraformGhostActive { get; private set; }
    public int TerraformGhostX { get; private set; }
    public int TerraformGhostY { get; private set; }
    public PlacementResult TerraformGhostResult { get; private set; }

    private bool _zoneDragging;
    private int _zoneStartX, _zoneStartY;

    // ----- přepínání nástrojů (jediné místo, kde se ruší ostatní) -----

    /// <summary>Vybere budovu ke stavbě; opětovná volba na stejnou volbu ruší.</summary>
    public void ToggleBuilding(int defIndex)
    {
        bool same = SelectedBuilding == defIndex;
        Clear();
        SelectedBuilding = same ? -1 : defIndex;
    }

    /// <summary>Zapne/vypne sázení.</summary>
    public void TogglePlant()
    {
        bool was = PlantMode;
        Clear();
        PlantMode = !was;
    }

    /// <summary>Zapne malování zón daného typu; stejný typ znovu = ven.</summary>
    public void ToggleZone(int typeIndex)
    {
        bool same = ZoneMode && ZonePaintTypeIndex == typeIndex;
        Clear();
        if (!same)
        {
            ZoneMode = true;
            ZonePaintTypeIndex = typeIndex;
        }
    }

    /// <summary>Zapne přetváření krajiny daným zásahem; stejný zásah znovu = ven.</summary>
    public void ToggleTerraform(int actionIndex)
    {
        bool same = TerraformIndex == actionIndex;
        Clear();
        TerraformIndex = same ? -1 : actionIndex;
    }

    /// <summary>Zapne slučování bloků; podruhé vypne.</summary>
    public void ToggleMerge()
    {
        bool was = MergeMode;
        Clear();
        MergeMode = !was;
    }

    /// <summary>
    /// Zapne práci se silnicemi; podruhé vypne.
    ///
    /// <para>Jedno tlačítko místo dvou: „stavět" a „bourat" jsou dva režimy
    /// jednoho nástroje, ne dva nástroje. Ve spodní liště je tak o tlačítko míň
    /// a přepíná se až uvnitř, kde to hráč zrovna řeší.</para>
    /// </summary>
    public void ToggleRoad()
    {
        bool was = RoadMode || RoadEraseMode;
        Clear();
        RoadMode = !was;
    }

    /// <summary>Přepne mezi kladením (+) a bouráním (−). Mimo režim silnic nedělá nic.</summary>
    public void SetRoadErasing(bool erasing)
    {
        if (!RoadMode && !RoadEraseMode)
        {
            return;
        }

        RoadMode = !erasing;
        RoadEraseMode = erasing;
    }

    /// <summary>Je zapnutý nástroj silnic (v kterémkoli z obou režimů)?</summary>
    public bool RoadToolActive => RoadMode || RoadEraseMode;

    /// <summary>Začne přesouvat budovu.</summary>
    public void StartMove(int buildingIndex)
    {
        Clear();
        MovingBuildingIndex = buildingIndex;
    }

    /// <summary>Vypne všechny nástroje (Escape, zavření stavebního menu).</summary>
    public void Clear()
    {
        SelectedBuilding = -1;
        PlantMode = false;
        ZoneMode = false;
        MovingBuildingIndex = -1;
        TerraformIndex = -1;
        MergeMode = false;
        RoadMode = false;
        RoadEraseMode = false;
        _roadDragging = false;
        _roadPath.Clear();
        MergeGhostActive = false;
        RoadGhostActive = false;
        _zoneDragging = false;
        _dragging = false;
        _plan.Clear();
        BulkBuildable = 0;
        GhostVisible = false;
        PlantGhostActive = false;
        MoveGhostActive = false;
        ZonePreviewActive = false;
        TerraformGhostActive = false;
    }

    /// <summary>
    /// Zruší jeden nástroj v pořadí podle „hloubky" (Escape). Vrací false, když
    /// nebylo co rušit — herní obrazovka pak otevře pauzu.
    /// </summary>
    public bool CancelTopmost()
    {
        if (RoadEraseMode) { RoadEraseMode = false; RoadGhostActive = false; return true; }
        if (RoadMode) { RoadMode = false; RoadGhostActive = false; return true; }
        if (MergeMode) { MergeMode = false; MergeGhostActive = false; return true; }
        if (TerraformIndex >= 0) { TerraformIndex = -1; TerraformGhostActive = false; return true; }
        if (ZoneMode) { ZoneMode = false; _zoneDragging = false; return true; }
        if (PlantMode) { PlantMode = false; return true; }
        if (MovingBuildingIndex >= 0) { MovingBuildingIndex = -1; return true; }
        if (SelectedBuilding >= 0) { SelectedBuilding = -1; return true; }
        return false;
    }

    // ----- tik -----

    /// <summary>
    /// Posune aktivní nástroj. Vrací true, pokud nástroj vstup „spotřeboval" —
    /// herní obrazovka pak neřeší ruční těžbu.
    /// </summary>
    public bool Update(bool mouseOverUi)
    {
        if (MergeMode)
        {
            UpdateMerge(mouseOverUi);
            return true;
        }

        if (RoadMode || RoadEraseMode)
        {
            UpdateRoad(mouseOverUi);
            return true;
        }

        if (TerraformIndex >= 0)
        {
            UpdateTerraform(mouseOverUi);
            return true;
        }

        if (ZoneMode)
        {
            UpdateZone(mouseOverUi);
            return true;
        }

        if (PlantMode)
        {
            UpdatePlant(mouseOverUi);
            return true;
        }

        if (UpdateMove(mouseOverUi))
        {
            return true;
        }

        UpdatePlacement(mouseOverUi);
        return false;
    }

    private void UpdatePlacement(bool mouseOverUi)
    {
        // Pravé tlačítko: krátký klik ruší výběr budovy, tažení je pan kamery.
        if (_input.WasRightPressed)
        {
            _rightDragDistance = 0f;
        }

        if (_input.IsRightDown)
        {
            _rightDragDistance += _input.MouseDelta.Length();
        }

        if (_input.WasRightReleased && _rightDragDistance < RightClickDragTolerance && SelectedBuilding >= 0)
        {
            SelectedBuilding = -1;
        }

        GhostVisible = false;
        if (SelectedBuilding < 0 || mouseOverUi)
        {
            _dragging = false;
            _plan.Clear();
            BulkBuildable = 0;
            return;
        }

        var def = _content.Buildings[SelectedBuilding];
        var (tileX, tileY) = TileUnderCursor();

        // Kurzor míří na střed půdorysu, ať se velké budovy pokládají přirozeně.
        GhostX = tileX - (def.FootprintWidth - 1) / 2;
        GhostY = tileY - (def.FootprintHeight - 1) / 2;
        GhostResult = _simulation.CanPlace(SelectedBuilding, GhostX, GhostY);
        GhostVisible = true;

        if (_input.WasLeftPressed)
        {
            _dragging = true;
            _dragStartX = GhostX;
            _dragStartY = GhostY;
        }

        if (_dragging && _input.IsLeftDown)
        {
            UpdateDragPlan();
            return;
        }

        if (_input.WasLeftReleased && _dragging)
        {
            CommitPlacement();
        }

        _dragging = false;
        _plan.Clear();
        BulkBuildable = 0;
    }

    /// <summary>
    /// Přepočítá náhled tažení. Mřížka je ukotvená v místě, kde tažení začalo —
    /// jinak by budovy pod kurzorem poskakovaly a hráč by netrefil, co chce.
    /// </summary>
    private void UpdateDragPlan()
    {
        if (!IsDragGesture())
        {
            _plan.Clear();
            BulkBuildable = 0;
            return;
        }

        BulkBuildable = _bulk.PlanArea(SelectedBuilding, _dragStartX, _dragStartY, GhostX, GhostY, _plan);
    }

    /// <summary>
    /// Postaví to, co hráč gestem vybral: tažením řadu či blok, jinak dávku podle
    /// násobiče kolem místa kliknutí.
    ///
    /// <para>Výběr budovy zůstává — idle hráč typicky staví víc budov za sebou.</para>
    /// </summary>
    private void CommitPlacement()
    {
        if (IsDragGesture())
        {
            _bulk.PlanArea(SelectedBuilding, _dragStartX, _dragStartY, GhostX, GhostY, _plan);
            _bulk.Build(SelectedBuilding, _plan);
            return;
        }

        if (BatchSize > 1)
        {
            _bulk.PlanNear(SelectedBuilding, GhostX, GhostY, BatchSize, _plan);
            _bulk.Build(SelectedBuilding, _plan);
            return;
        }

        if (GhostResult == PlacementResult.Ok)
        {
            _simulation.TryPlaceBuilding(SelectedBuilding, GhostX, GhostY);
        }
    }

    /// <summary>Ujel kurzor od stisku dost na to, aby šlo o tažení, ne o klik?</summary>
    private bool IsDragGesture() =>
        Math.Abs(GhostX - _dragStartX) >= DragThresholdTiles || Math.Abs(GhostY - _dragStartY) >= DragThresholdTiles;

    /// <summary>
    /// Přetváření krajiny: náhled sleduje kurzor a barva říká, jestli zásah projde.
    /// Levý klik přetvoří dlaždici (za cenu), pravý režim opustí. V režimu se
    /// zůstává — hráč obvykle upravuje víc dlaždic za sebou.
    /// </summary>
    /// <summary>
    /// Slučování: pod kurzorem se ukáže obrys bloku 2×2, který by se sloučil.
    /// Ghost je celý blok, ne jedna dlaždice — hráč tak vidí, co přesně zmizí.
    /// </summary>
    private void UpdateMerge(bool mouseOverUi)
    {
        MergeGhostActive = false;
        if (_input.WasRightPressed)
        {
            MergeMode = false;
            return;
        }

        if (mouseOverUi)
        {
            return;
        }

        var (x, y) = TileUnderCursor();
        if (!_simulation.TryFindMergeGroup(x, y, out var group))
        {
            return;
        }

        MergeGhostX = group.X;
        MergeGhostY = group.Y;
        MergeGhostResult = _simulation.CanMerge(group);
        MergeGhostActive = true;

        if (_input.WasLeftPressed && MergeGhostResult == PlacementResult.Ok)
        {
            _simulation.TryMerge(x, y);
        }
    }

    /// <summary>
    /// Ruční silnice: levý klik pokládá (nebo bourá v režimu bourání) a drženým
    /// tlačítkem jde táhnout ulici. Tažení je tu podstatné — po jedné dlaždici
    /// by se rovná ulice stavěla nesnesitelně dlouho.
    /// </summary>
    private void UpdateRoad(bool mouseOverUi)
    {
        RoadGhostActive = false;
        if (_input.WasRightPressed)
        {
            RoadMode = false;
            RoadEraseMode = false;
            _roadDragging = false;
            _roadPath.Clear();
            return;
        }

        if (mouseOverUi)
        {
            return;
        }

        var (x, y) = TileUnderCursor();
        RoadGhostX = x;
        RoadGhostY = y;
        RoadGhostErasing = RoadEraseMode;
        RoadGhostResult = RoadEraseMode
            ? _simulation.IsRoad(x, y) ? PlacementResult.Ok : PlacementResult.Occupied
            : _simulation.CanBuildRoad(x, y);
        RoadGhostActive = true;

        // Stisk určí začátek úseku, puštění konec. Dokud hráč drží, je vidět celá
        // budoucí ulice — dřív se pokládalo po jedné dlaždici pod kurzorem, takže
        // rychlý tah nechal v cestě díry a rovnou ulici nešlo trefit vůbec.
        if (_input.WasLeftPressed)
        {
            _roadDragging = true;
            _roadAnchorX = x;
            _roadAnchorY = y;
        }

        if (!_roadDragging)
        {
            _roadPath.Clear();
            return;
        }

        TracePath(_roadAnchorX, _roadAnchorY, x, y);

        if (_input.IsLeftDown)
        {
            return; // pořád táhne — jen náhled
        }

        foreach (var (tileX, tileY) in _roadPath)
        {
            if (RoadEraseMode)
            {
                _simulation.TryRemoveRoad(tileX, tileY);
            }
            else
            {
                _simulation.TryBuildRoad(tileX, tileY);
            }
        }

        _roadDragging = false;
        _roadPath.Clear();
    }

    /// <summary>
    /// Trasa z bodu A do bodu B do „L": nejdřív po delší ose, pak po druhé.
    /// Lomená cesta je to, co hráč od tažení čeká — úhlopříčka po dlaždicích
    /// vypadá jako schody a v mřížkovém městě se nehodí.
    /// </summary>
    private void TracePath(int fromX, int fromY, int toX, int toY)
    {
        _roadPath.Clear();

        bool horizontalFirst = Math.Abs(toX - fromX) >= Math.Abs(toY - fromY);
        int x = fromX;
        int y = fromY;
        _roadPath.Add((x, y));

        if (horizontalFirst)
        {
            while (x != toX) { x += Math.Sign(toX - x); _roadPath.Add((x, y)); }
            while (y != toY) { y += Math.Sign(toY - y); _roadPath.Add((x, y)); }
        }
        else
        {
            while (y != toY) { y += Math.Sign(toY - y); _roadPath.Add((x, y)); }
            while (x != toX) { x += Math.Sign(toX - x); _roadPath.Add((x, y)); }
        }
    }

    private void UpdateTerraform(bool mouseOverUi)
    {
        TerraformGhostActive = false;
        if (_input.WasRightPressed)
        {
            TerraformIndex = -1;
            return;
        }

        if (mouseOverUi)
        {
            return;
        }

        (TerraformGhostX, TerraformGhostY) = TileUnderCursor();
        TerraformGhostResult = _simulation.CanTerraform(TerraformIndex, TerraformGhostX, TerraformGhostY);
        TerraformGhostActive = true;

        if (_input.WasLeftPressed && TerraformGhostResult == PlacementResult.Ok)
        {
            _simulation.TryTerraform(TerraformIndex, TerraformGhostX, TerraformGhostY);
        }
    }

    /// <summary>Sázení: ghost háje sleduje kurzor, levý klik zasadí (za cenu), pravý ruší.</summary>
    private void UpdatePlant(bool mouseOverUi)
    {
        PlantGhostActive = false;
        if (_input.WasRightPressed)
        {
            PlantMode = false;
            return;
        }

        if (mouseOverUi)
        {
            return;
        }

        (PlantGhostX, PlantGhostY) = TileUnderCursor();
        PlantGhostResult = _simulation.CanPlant(PlantGhostX, PlantGhostY);
        PlantGhostActive = true;

        if (_input.WasLeftPressed && PlantGhostResult == PlacementResult.Ok)
        {
            _simulation.TryPlant(PlantGhostX, PlantGhostY); // zůstáváme v režimu — sázej dál
        }
    }

    /// <summary>
    /// Zóny: levým tažením se maluje obdélník (vznikne po puštění), pravý klik
    /// smaže zónu pod kurzorem (nebo — do prázdna — vyjde z režimu).
    /// </summary>
    private void UpdateZone(bool mouseOverUi)
    {
        ZonePreviewActive = false;
        var (tileX, tileY) = TileUnderCursor();

        if (_input.WasRightPressed)
        {
            if (_zoneDragging)
            {
                _zoneDragging = false; // zruší rozdělanou malbu
            }
            else if (mouseOverUi || !_simulation.RemoveZoneAt(tileX, tileY))
            {
                ZoneMode = false; // klik do prázdna (nebo přes UI) → ven z režimu
            }

            return;
        }

        if (mouseOverUi && !_zoneDragging)
        {
            return;
        }

        if (_input.WasLeftPressed && !mouseOverUi)
        {
            _zoneDragging = true;
            _zoneStartX = tileX;
            _zoneStartY = tileY;
        }

        if (!_zoneDragging)
        {
            return;
        }

        int x = Math.Min(_zoneStartX, tileX);
        int y = Math.Min(_zoneStartY, tileY);
        int width = Math.Abs(tileX - _zoneStartX) + 1;
        int height = Math.Abs(tileY - _zoneStartY) + 1;
        ZonePreview = new Rectangle(x, y, width, height);
        ZonePreviewActive = true;

        if (_input.WasLeftReleased)
        {
            _simulation.AddZone(ZonePaintTypeIndex, x, y, width, height);
            _zoneDragging = false; // zůstáváme v režimu — maluj další zónu
        }
    }

    /// <summary>
    /// Přesun budovy: ghost sleduje kurzor, levý klik potvrdí (zdarma), pravý ruší.
    /// Vrací true, dokud režim běží — potlačí stavbu i těžbu.
    /// </summary>
    private bool UpdateMove(bool mouseOverUi)
    {
        MoveGhostActive = false;
        if (MovingBuildingIndex < 0)
        {
            return false;
        }

        // Budova mohla mezitím zmizet (jiný zdroj) — z režimu ven.
        if (MovingBuildingIndex >= _simulation.Buildings.Length)
        {
            MovingBuildingIndex = -1;
            return false;
        }

        if (_input.WasRightPressed)
        {
            MovingBuildingIndex = -1;
            return true;
        }

        if (mouseOverUi)
        {
            return true;
        }

        var def = _content.Buildings[_simulation.Buildings[MovingBuildingIndex].DefIndex];
        var (tileX, tileY) = TileUnderCursor();
        MoveGhostX = tileX - (def.FootprintWidth - 1) / 2;
        MoveGhostY = tileY - (def.FootprintHeight - 1) / 2;
        MoveGhostResult = _simulation.CanMoveBuilding(MovingBuildingIndex, MoveGhostX, MoveGhostY);
        MoveGhostActive = true;

        if (_input.WasLeftPressed && MoveGhostResult == PlacementResult.Ok)
        {
            _simulation.TryMoveBuilding(MovingBuildingIndex, MoveGhostX, MoveGhostY);
            MovingBuildingIndex = -1;
            MoveGhostActive = false;
        }

        return true;
    }

    private (int X, int Y) TileUnderCursor()
    {
        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        return ((int)MathF.Floor(world.X / TerrainRenderer.TileSize),
                (int)MathF.Floor(world.Y / TerrainRenderer.TileSize));
    }
}
