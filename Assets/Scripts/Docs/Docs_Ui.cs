/**
 * @file Docs_Ui.cs
 * @brief Documentation entry for the User Interface (UI) subsystem.
 *
 * @defgroup ui UI
 * @ingroup systems
 * @brief Runtime UI widgets, menus, overlays, previews, and utilities for level browsing and play.
 *
 * @details
 * The UI layer is a set of MonoBehaviours and strategy objects that render status, preview
 * content, and route player selections into gameplay systems. It emphasizes decoupling
 * (button actions as strategies), async-safe previews, and resolution-independent layout
 * for scrollable level grids.
 *
 * Contents:
 * - see ui_overview
 * - see ui_components
 * - see ui_lifecycle
 * - see ui_usage
 * - see ui_api
 * - see ui_integration
 * - see ui_performance
 * - see ui_troubleshooting
 * - see ui_versions
 *
 * ----------------------------------------------------------------------
 * @section ui_overview Overview
 *
 * Responsibilities:
 * - Parallax: UIParallax animates layered HUD elements from pointer position.
 * - Menus/Buttons: ButtonScript delegates clicks to serialized ButtonType strategies
 *   such as ChangeSceneButton and GenerateLevelButton.
 * - Level List: LevelScrollContent builds a responsive grid of LevelEntry items.
 * - Thumbnails: LevelPreviewer renders a texture preview of a LevelMap off-thread.
 * - Overlays: RaceOverLay shows timers, lap/checkpoint counts, and finish screen.
 * - Popups: LevelPopUp shows a generated level, with keep or dismiss actions.
 * - Dropdowns: DropDownMenuButtonSceneChanger mirrors a TMP_Dropdown into a large button.
 *
 * Dependencies:
 * - TextMeshPro (TMP) for labels and dropdowns.
 * - Unity UI (UGUI) for buttons, images, and layouts.
 * - GameDataManager for level data and selections.
 * - TrackManager for race state (timers, laps, checkpoints).
 * - SceneManagement and SceneAssetHelper for scene changes.
 *
 * Threading:
 * - Unity main thread for all MonoBehaviour lifecycles and UI updates.
 * - Background threads are used by LevelPreviewer (buffer construction) and
 *   GenerateLevelButton (level generation and checkpoint placement); results
 *   are applied on the main thread.
 *
 * Invariants:
 * - Public UI flows never block the main thread; long-running work uses tasks.
 * - UI scripts validate required references in Awake/Start/OnValidate and log errors if missing.
 *
 * ----------------------------------------------------------------------
 * @section ui_components Components
 *
 * Parallax:
 * - UIParallaxLayer: RectTransform, Strength (Vector2), TiltZ, InvertedTilt flags.
 * - UIParallax: per-frame pointer-to-canvas normalization, anchoredPosition lerp, optional Z-tilt.
 *
 * Buttons (Strategy):
 * - ButtonScript: hosts a serialized ButtonType and invokes it on click.
 * - ChangeSceneButton: loads a target SceneAssetHelper via SceneManagement.
 * - SelectLevelButton: selects a LevelMap in GameDataManager.
 * - RemoveLevelButton: removes a LevelMap from GameDataManager.
 * - GenerateLevelButton: background-generate LevelMap, add checkpoints, show LevelPopUp.
 * - ClearLevelsButton: clears all stored levels.
 * - GoToSelectedLevel: transitions to the currently selected level.
 *
 * Lists & Layout:
 * - LevelScrollContent: responsive, square-tile grid of LevelEntry items; uses viewport-width
 *   ratios for gaps and paddings; recomputes on rect changes.
 * - LevelEntry: binds a thumbnail and time label; forwards selection or removal to GameDataManager.
 *
 * Rendering & Preview:
 * - LevelPreviewer: builds a Color32 buffer for grass, road, start, finish, checkpoint on a worker
 *   thread; then uploads it to a point-filtered Texture2D for a RawImage target.
 * - LevelPopUp: hosts LevelPreviewer; show, hide, or keep the generated LevelMap.
 *
 * Overlays:
 * - RaceOverLay: reads TrackManager for lap time, total time, and countdown; updates TMP fields
 *   and toggles start or finish panels.
 *
 * Dropdown/Button Hybrid:
 * - DropDownMenuButtonSceneChanger: styles a TMP_Dropdown, mirrors selection into a large
 *   Button label, and executes the mapped SceneAssetHelper when clicked.
 *
 * Utility:
 * - SliderScript: writes a rounded two-digit value to a TMP label on slider change.
 *
 * ----------------------------------------------------------------------
 * @section ui_lifecycle Lifecycle
 *
 * Initialization:
 * - Components validate references in OnValidate and Awake.
 * - LevelScrollContent subscribes to GameDataManager changes and populates entries.
 *
 * Runtime:
 * - UIParallax reads Input.mousePosition each Update and lerps layers with damping.
 * - RaceOverLay polls TrackManager each frame for times and finish state.
 * - LevelScrollContent recomputes layout on OnRectTransformDimensionsChange.
 * - LevelPreviewer and GenerateLevelButton perform CPU-heavy work on background threads,
 *   then apply textures and UI changes on the main thread.
 *
 * Teardown:
 * - LevelScrollContent removes its GameDataManager listener in OnDestroy.
 *
 * ----------------------------------------------------------------------
 * @section ui_usage Usage
 *
 * Parallax:
 * @code{.cs}
 * // Attach UIParallax under a Canvas. Add layers in the Inspector and set Strength and optional TiltZ.
 * @endcode
 *
 * Button strategy:
 * @code{.cs}
 * // In the Inspector, set ButtonScript.Properties to a GenerateLevelButton and wire popUp/slider refs.
 * // Then hook the Button's OnClick to ButtonScript.OnButtonClick().
 * @endcode
 *
 * Level list:
 * @code{.cs}
 * // Place LevelScrollContent under a ScrollRect's content. Assign levelPreviewerPrefab (with LevelEntry).
 * // It auto-populates from GameDataManager.Instance.CurrentGameData.Levels.
 * @endcode
 *
 * Previews and popup:
 * @code{.cs}
 * // Acquire or generate a LevelMap, then:
 * popupGameObject.GetComponent<LevelPopUp>().ShowMap(map);
 * // Use KeepMap to persist via GameDataManager; HideMap to clear and close.
 * @endcode
 *
 * Overlay:
 * @code{.cs}
 * // Place RaceOverLay in the scene; assign TMP fields and optionally TrackManager.
 * // The overlay auto-updates lap/track times and finish visibility.
 * @endcode
 *
 * Dropdown-driven scene:
 * @code{.cs}
 * // Use DropDownMenuButtonSceneChanger, assign TMP_Dropdown, arrow sprite, colors, and SceneAssetHelper[].
 * // The big button shows the current dropdown option and runs the mapped scene action.
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section ui_api Public API Reference (selected)
 *
 * UIParallax
 * - void Update(): computes normalized pointer and lerps each layer's anchoredPosition and rotation.
 *
 * ButtonScript
 * - ButtonType Properties { get; set; }
 * - void OnButtonClick(): executes Properties.Action().
 *
 * GenerateLevelButton
 * - bool createCircuits, int sizeX, int sizeY, int stepCount
 * - void Action(): task-based generation and checkpointing, then LevelPopUp.ShowMap.
 *
 * LevelScrollContent
 * - void Start(): subscribe and build entries.
 * - void OnRectTransformDimensionsChange(): recompute layout.
 *
 * LevelPreviewer
 * - System.Threading.Tasks.Task ShowPreviewAsync(LevelMap map): async buffer build and texture upload.
 * - void Clear(): release texture reference.
 *
 * LevelPopUp
 * - void ShowMap(LevelMap map): display preview.
 * - void HideMap(): clear and close.
 * - void KeepMap(): persist via GameDataManager.AddLevel.
 *
 * RaceOverLay
 * - void Update(): read timers and flags from TrackManager and update TMP fields.
 *
 * DropDownMenuButtonSceneChanger
 * - void OnChange(int index): mirror selection name to the big button.
 * - void OnButtonClicked(): run mapped SceneAssetHelper.
 *
 * SliderScript
 * - void OnChange(float newValue): write rounded two-digit text to TMP.
 *
 * ----------------------------------------------------------------------
 * @section ui_integration Integration Notes
 *
 * - Data: GameDataManager supplies level lists and accepts selections or additions.
 * - Race state: RaceOverLay expects a TrackManager in the scene for timing, laps, and checkpoints.
 * - Scenes: ChangeSceneButton and DropDownMenuButtonSceneChanger depend on SceneManagement.
 * - Input: UIParallax supports both ScreenSpaceOverlay and Camera-based canvases.
 *
 * ----------------------------------------------------------------------
 * @section ui_performance Performance and GC
 *
 * - LevelPreviewer and GenerateLevelButton use background threads for heavy work; avoid calling
 *   them every frame. Cache previews when repeatedly shown.
 * - LevelScrollContent uses viewport-width ratios to remain resolution-independent; layout
 *   recalculation is O(N) in item count.
 * - UIParallax applies an exponential lerp per layer; keep layer counts modest.
 *
 * ----------------------------------------------------------------------
 * @section ui_troubleshooting Troubleshooting
 *
 * - Blank preview: ensure the RawImage target is assigned and the LevelMap dimensions are positive.
 * - Missing texts: verify TMP references on RaceOverLay and that a TrackManager is present.
 * - Dropdown button not updating: call OnChange(-1) on Reset or Validate and ensure the
 *   SceneAssetHelper array length matches TMP options.
 * - Button has no effect: assign ButtonScript.Properties in the Inspector to a concrete ButtonType.
 *
 * ----------------------------------------------------------------------
 * @section ui_versions Version History
 *
 * - v1.4: Added SliderScript for TMP-linked sliders; minor doc updates.
 * - v1.3: Added LevelSelection Scripts (LevelEntry, LevelScrollContent, LevelPreviewer, etc.).
 * - v1.2: Rewamped ButtonScript to use strategy pattern; added GenerateLevelButton.
 * - v1.1: Adding RaceOverLay and async preview generation.
 * - v1.0: General button script, DropDownMenuButtonSceneChanger.
 */
