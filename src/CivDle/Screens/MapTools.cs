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

    private readonly Simulation _simulation;
    private readonly Camera2D _camera;
    private readonly InputManager _input;
    private readonly CivDle.Core.Content.GameContent _content;

    private float _rightDragDistance;

    public MapTools(Simulation simulation, Camera2D camera, InputManager input, CivDle.Core.Content.GameContent content)
    {
        _simulation = simulation;
        _camera = camera;
        _input = input;
        _content = content;
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

    /// <summary>Je aktivní jakýkoli nástroj? (Jinak klik na mapu znamená těžbu.)</summary>
    public bool AnyActive => SelectedBuilding >= 0 || PlantMode || ZoneMode || MovingBuildingIndex >= 0;

    // ----- duch pod kurzorem (čte render) -----

    public bool GhostVisible { get; private set; }
    public int GhostX { get; private set; }
    public int GhostY { get; private set; }
    public PlacementResult GhostResult { get; private set; }

    public bool MoveGhostActive { get; private set; }
    public int MoveGhostX { get; private set; }
    public int MoveGhostY { get; private set; }
    public PlacementResult MoveGhostResult { get; private set; }

    public bool PlantGhostActive { get; private set; }
    public int PlantGhostX { get; private set; }
    public int PlantGhostY { get; private set; }
    public PlacementResult PlantGhostResult { get; private set; }

    public bool ZonePreviewActive { get; private set; }
    public Rectangle ZonePreview { get; private set; }

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
        _zoneDragging = false;
        GhostVisible = false;
        PlantGhostActive = false;
        MoveGhostActive = false;
        ZonePreviewActive = false;
    }

    /// <summary>
    /// Zruší jeden nástroj v pořadí podle „hloubky" (Escape). Vrací false, když
    /// nebylo co rušit — herní obrazovka pak otevře pauzu.
    /// </summary>
    public bool CancelTopmost()
    {
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
            return;
        }

        var def = _content.Buildings[SelectedBuilding];
        var (tileX, tileY) = TileUnderCursor();

        // Kurzor míří na střed půdorysu, ať se velké budovy pokládají přirozeně.
        GhostX = tileX - (def.FootprintWidth - 1) / 2;
        GhostY = tileY - (def.FootprintHeight - 1) / 2;
        GhostResult = _simulation.CanPlace(SelectedBuilding, GhostX, GhostY);
        GhostVisible = true;

        if (_input.WasLeftPressed && GhostResult == PlacementResult.Ok)
        {
            // Výběr zůstává — idle hráč typicky staví víc budov za sebou.
            _simulation.TryPlaceBuilding(SelectedBuilding, GhostX, GhostY);
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
