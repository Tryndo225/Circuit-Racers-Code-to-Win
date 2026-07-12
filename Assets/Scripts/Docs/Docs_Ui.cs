/**
 * @file Docs_Ui.cs
 * @brief Documentation entry for the User Interface subsystem.
 *
 * @defgroup ui User Interface
 * @ingroup systems
 * @brief Runtime UI widgets, menus, overlays, popups, settings, notifications, previews, and button actions.
 *
 * @details
 * The User Interface subsystem contains runtime MonoBehaviours and serialized button strategies that connect
 * player-facing UI to the rest of the game. It covers menu buttons, level generation popups, race overlays,
 * settings panels, notifications, parallax effects, dropdown scene buttons, sliders, and level editor controls.
 *
 * Level-list-specific UI is also documented in this file through the nested ::ui_levels group.
 *
 * Contents:
 * - @ref ui_overview
 * - @ref ui_groups
 * - @ref ui_components
 * - @ref ui_lifecycle
 * - @ref ui_usage
 * - @ref ui_api
 * - @ref ui_integration
 * - @ref ui_performance
 * - @ref ui_troubleshooting
 * - @ref ui_versions
 *
 * ----------------------------------------------------------------------
 * @section ui_overview Overview
 *
 * Responsibilities:
 * - Route menu/button clicks through serialized ::ButtonType strategies.
 * - Generate new levels asynchronously and show them in a preview popup.
 * - Import and export levels through clipboard-based share strings.
 * - Display saved level entries, best times, names, previews, and day/night markers.
 * - Provide a manual level editor for painting tiles and editing metadata.
 * - Render race HUD information from a generic ::RaceOverlaySource.
 * - Support both live-race and replay HUD data sources.
 * - Display checkpoint split feedback and finish/unfinished result screens.
 * - Synchronize ABS and traction-control settings through ::SettingsPanel.
 * - Show transient notifications.
 * - Provide small UI effects such as parallax, fading, dropdown-driven scene buttons, and slider labels.
 *
 * Dependencies:
 * - TextMeshPro for labels, input fields, and dropdown text.
 * - Unity UI for buttons, sliders, toggles, panels, RawImage, and layout.
 * - ::GameDataManager for saved levels, selected level, assists, and replays.
 * - ::ImportExportManager for level import/export.
 * - ::LevelGenerator, ::SeedFactory, and ::LevelCheckPointMaker for generated level creation.
 * - ::LevelMapValidator for level editor validation.
 * - ::SceneManagement and ::SceneAssetHelper for scene transitions.
 * - ::TrackManager, ::RaceTimeManager, and ::CheckPointManager for live race overlay data.
 * - ::ReplayPreviewer for replay overlay data.
 *
 * Threading:
 * - Unity UI updates run on the Unity main thread.
 * - ::GenerateLevelButton uses Task.Run for generation and checkpoint creation.
 * - ::LevelPreviewer builds preview pixel buffers asynchronously and uploads the Texture2D on the main thread.
 *
 * Design notes:
 * - Button behavior is strategy-based through ::ButtonType.
 * - Race overlay display is source-based through ::RaceOverlaySource.
 * - Saved-level list UI is separated into the ::ui_levels group.
 *
 * ----------------------------------------------------------------------
 * @section ui_groups UI Groups
 *
 * Main UI group:
 * - ::ui contains general menu, overlay, settings, notification, popup, parallax, dropdown, slider,
 *   button-strategy, and editor UI scripts.
 *
 * Level UI group:
 * - ::ui_levels contains saved-level browsing/list-entry components such as ::LevelEntry and
 *   ::LevelScrollContent.
 *
 * Cross-group scripts:
 * - Some UI scripts also belong to groups such as ::game_data, ::level_gen, ::scene_mgmt,
 *   ::track_mng, or ::replay_system because they bridge UI to those systems.
 *
 * ----------------------------------------------------------------------
 * @section ui_components Components
 *
 * Parallax:
 * - ::UIParallaxLayer stores a RectTransform, movement strength, tilt amount, and tilt inversion flag.
 * - ::UIParallax reads pointer position in canvas space and eases layers toward offset and rotation targets.
 *
 * Button strategies:
 * - ::ButtonScript stores a serialized ::ButtonType and calls its Action method.
 * - ::ButtonType is the abstract base class for serialized UI actions.
 * - ::ChangeSceneButton loads a configured ::SceneAssetHelper through ::SceneManagement.
 * - ::SelectLevelButton selects a concrete ::levelMap in ::GameDataManager.
 * - ::RemoveLevelButton removes a concrete ::levelMap from ::GameDataManager.
 * - ::GenerateLevelButton generates a level asynchronously, adds checkpoints, and displays it through ::LevelPopUp.
 * - ::ClearLevelsButton clears all saved custom levels.
 * - ::GoToSelectedLevel delegates gameplay transition to ::GameDataManager::GoToSelectedLevel.
 * - ::ClosePopUpButton closes a configured popup GameObject.
 * - ::OpenPopUpButton opens a configured popup GameObject.
 * - ::ImportLevelFromClipboardButton tries clipboard import and opens a manual popup on failure.
 * - ::ImportLevelButton imports a level from a TMP input field.
 * - ::ReplayButton opens the replay scene only when a best replay exists.
 * - ::EditButton opens the editor scene only when a level is selected.
 * - ::NotificationButton displays a configured notification.
 * - ::GoPlayButtom selects a day or night gameplay scene based on the selected level's IsDayTrack flag.
 *
 * Level browsing:
 * - ::LevelScrollContent creates a responsive grid of saved-level entries.
 * - ::LevelEntry displays one saved level's name, best time, preview, and day/night icon.
 * - ::LevelEntry also exposes callbacks for selecting, exporting, renaming, and removing the level.
 *
 * Level preview and popup:
 * - ::LevelPreviewer renders a color-coded preview texture from a ::levelMap.
 * - ::LevelPopUp shows or hides a generated level preview and can add the previewed level to saved data.
 *
 * Level editor:
 * - ::LevelEditorManager edits an editable ::levelMap copy.
 * - It supports painting grass, track, and checkpoint tiles.
 * - It supports moving start/finish points, changing circuit mode, day/night mode, lap count, and level name.
 * - It can regenerate checkpoints, validate maps, save new levels, or replace edited levels.
 *
 * Race overlay:
 * - ::RaceOverlayState is a data snapshot for HUD values.
 * - ::RaceOverlaySource is the abstract source interface for overlay data.
 * - ::TrackRaceOverlaySource adapts ::TrackManager, ::RaceTimeManager, and ::CheckPointManager into overlay state.
 * - ::ReplayRaceOverlaySource adapts ::ReplayPreviewer into overlay state.
 * - ::RaceOverlay renders countdowns, lap/track times, counters, checkpoint split popups, and result screens.
 *
 * Settings:
 * - ::SettingsPanel synchronizes ABS and traction-control toggles with ::GameDataManager.
 *
 * Notifications:
 * - ::NotificationManager displays transient notification popups.
 * - ::NotificationPopup controls the visible popup instance.
 *
 * Scene dropdown:
 * - ::DropDownMenuButtonSceneChanger connects a TMP_Dropdown to a larger scene-action button.
 *
 * Utility widgets:
 * - ::SliderScript writes rounded slider values to a TMP label.
 * - ::FadingScreen provides screen fade behaviour when configured in menus or scene transitions.
 *
 * ----------------------------------------------------------------------
 * @section ui_lifecycle Lifecycle
 *
 * ::ButtonScript:
 * - OnButtonClick checks that a strategy is assigned.
 * - If assigned, it calls ButtonType.Action().
 * - Missing button configuration is reported as a warning instead of throwing.
 *
 * ::GenerateLevelButton:
 * - Reads size and circuit settings from optional UI widgets.
 * - Computes generation parameters from the selected size.
 * - Runs ::LevelGenerator on a background task.
 * - Runs ::LevelCheckPointMaker on a background task.
 * - Shows the resulting map through ::LevelPopUp on the main thread.
 * - Catches and logs generation errors.
 *
 * ::LevelScrollContent:
 * - Subscribes to ::GameDataManager change notifications in Start.
 * - Creates one ::LevelEntry prefab instance per saved level.
 * - Rebuilds entries when saved-level data changes.
 * - Recomputes grid layout when the RectTransform size changes.
 * - Unsubscribes in OnDestroy.
 *
 * ::LevelEntry:
 * - Receives data through SetUp(GameDataManager::LevelData).
 * - Updates level name, best-time text, day/night icon, and optional preview.
 * - Uses public callbacks for select, export, remove, and rename actions.
 *
 * ::LevelPreviewer:
 * - Creates a preview texture from levelMap tile data.
 * - Uses point filtering for pixel-like previews.
 * - Clears old texture references when requested.
 *
 * ::LevelPopUp:
 * - Hides itself on Start.
 * - ShowMap stores the previewed levelMap, activates the visual container, and asks LevelPreviewer to render it.
 * - HideMap clears the preview and hides the container.
 * - KeepMap adds the current map to ::GameDataManager and closes the popup.
 *
 * ::LevelEditorManager:
 * - Creates an editable copy of the currently selected level.
 * - Synchronizes UI controls with the editable copy.
 * - Converts pointer clicks on the preview grid into tile coordinates.
 * - Updates the preview after changes.
 * - Saves only valid maps.
 *
 * ::RaceOverlay:
 * - Backfills a ::RaceOverlaySource in OnValidate and Start when none is assigned.
 * - Clears labels and hides panels on Start.
 * - Each Update asks the source for ::RaceOverlayState.
 * - Updates countdown, timers, lap counter, checkpoint counter, and finish screen.
 * - Stops normal updating after the finish/unfinished screen has been opened.
 *
 * ::TrackRaceOverlaySource:
 * - Backfills ::TrackManager from the scene singleton.
 * - Reads timing from ::RaceTimeManager.
 * - Reads checkpoint totals from ::CheckPointManager.
 * - Formats live lap/checkpoint counters and final time text.
 *
 * ::ReplayRaceOverlaySource:
 * - Backfills ::ReplayPreviewer from the scene singleton.
 * - Reports replay time as both lap time and track time.
 * - Uses "Replay" as the lap counter text.
 * - Marks the overlay finished when replay playback reaches the end.
 *
 * ::SettingsPanel:
 * - Hides itself on Start.
 * - OnEnable reads saved ABS and TC settings from ::GameDataManager.
 * - SetABS and SetTC write changed toggle values back to saved settings.
 *
 * ::UIParallax:
 * - Caches the parent canvas and initial layer transforms in Awake.
 * - In Update, converts pointer position into normalized canvas space.
 * - Applies unscaled-time exponential smoothing to layer positions and optional Z rotation.
 *
 * ----------------------------------------------------------------------
 * @section ui_usage Usage
 *
 * Button strategy:
 * @code{.cs}
 * // Add ButtonScript to a UI button object.
 * // Assign a concrete ButtonType in the Inspector.
 * // Hook the Unity Button OnClick event to ButtonScript.OnButtonClick().
 * @endcode
 *
 * Generate a level from UI:
 * @code{.cs}
 * // Assign GenerateLevelButton to ButtonScript.Properties.
 * // Assign the popup GameObject, size slider, and circuit toggle.
 * // On click, generation runs asynchronously and the resulting map is shown in LevelPopUp.
 * @endcode
 *
 * Show a generated map popup:
 * @code{.cs}
 * LevelPopUp popup = popupObject.GetComponent<LevelPopUp>();
 * popup.ShowMap(generatedMap);
 * @endcode
 *
 * Keep or discard a generated map:
 * @code{.cs}
 * popup.KeepMap(); // adds to GameDataManager and closes
 * popup.HideMap(); // discards preview and closes
 * @endcode
 *
 * Saved level list:
 * @code{.cs}
 * // Place LevelScrollContent on the ScrollRect content object.
 * // Assign contentRect and a prefab containing LevelEntry.
 * // The list rebuilds from GameDataManager.Instance.CurrentGameData.Levels.
 * @endcode
 *
 * Level entry callbacks:
 * @code{.cs}
 * LevelEntry entry = levelEntryObject.GetComponent<LevelEntry>();
 * entry.LevelSelected();
 * entry.ExportLevelButton();
 * entry.RemoveLevel();
 * entry.OnLevelNameChange("New Name");
 * @endcode
 *
 * Race overlay:
 * @code{.cs}
 * // Add RaceOverlay to the HUD.
 * // Add TrackRaceOverlaySource for live gameplay or ReplayRaceOverlaySource for replay playback.
 * // Assign the source to RaceOverlay, or let it be found automatically.
 * @endcode
 *
 * Display a split popup:
 * @code{.cs}
 * RaceOverlay.Instance.DisplaySplit(splitTime, splitDifference);
 * @endcode
 *
 * Settings panel:
 * @code{.cs}
 * // Assign ABS and TC toggles.
 * // Connect toggle OnValueChanged events to SettingsPanel.SetABS and SettingsPanel.SetTC.
 * @endcode
 *
 * Parallax:
 * @code{.cs}
 * // Attach UIParallax under a Canvas.
 * // Add UIParallaxLayer entries and assign RectTransform references.
 * @endcode
 *
 * ----------------------------------------------------------------------
 * @section ui_api Public API Reference
 *
 * ::ButtonScript:
 * - ButtonType Properties
 *   Gets or sets the serialized button strategy.
 *
 * - void OnButtonClick()
 *   Executes the assigned button strategy if one exists.
 *
 * ::ButtonType:
 * - void Action()
 *   Executes the strategy-specific action.
 *
 * ::LevelEntry:
 * - void SetUp(GameDataManager::LevelData levelData)
 *   Initializes the level entry from saved level data.
 *
 * - void LevelSelected()
 *   Selects this entry's level through ::GameDataManager.
 *
 * - void ExportLevelButton()
 *   Exports this entry's level through ::ImportExportManager.
 *
 * - void RemoveLevel()
 *   Removes this entry's level through ::GameDataManager.
 *
 * - void OnLevelNameChange(string name)
 *   Updates the stored level name.
 *
 * ::LevelPreviewer:
 * - Task ShowPreviewAsync(levelMap map)
 *   Renders a preview for the provided map.
 *
 * - void Clear()
 *   Clears the displayed preview texture reference.
 *
 * ::LevelPopUp:
 * - void ShowMap(levelMap map)
 *   Shows the popup and previews the provided map.
 *
 * - void HideMap()
 *   Clears the previewed map and hides the popup.
 *
 * - void KeepMap()
 *   Adds the previewed map to saved game data and closes the popup.
 *
 * ::LevelEditorManager:
 * - void OnGrassSelected()
 *   Selects grass painting mode.
 *
 * - void OnTrackSelected()
 *   Selects track painting mode.
 *
 * - void OnCheckPointSelected()
 *   Selects checkpoint painting mode.
 *
 * - void OnStartSelected()
 *   Selects start-point placement mode.
 *
 * - void OnFinishSelected()
 *   Selects finish-point placement mode.
 *
 * - void SetCircuit(bool isCircuit)
 *   Sets whether the edited level is a circuit.
 *
 * - void SetDayNight(bool isNight)
 *   Sets the edited level day/night flag.
 *
 * - void SetLaps(float laps)
 *   Sets the edited level lap count.
 *
 * - void OnLevelNameChange(string name)
 *   Updates the edited level name.
 *
 * - void AutomaticCPGeneration()
 *   Regenerates checkpoints for the edited map and refreshes preview.
 *
 * ::RaceOverlaySource:
 * - bool TryGetState(out RaceOverlayState state)
 *   Attempts to provide current overlay state.
 *
 * ::RaceOverlay:
 * - void DisplaySplit(float splitTime, float splitDiff)
 *   Displays a temporary checkpoint split popup.
 *
 * - static string FormatTime(float time)
 *   Formats seconds into race-time text.
 *
 * ::SettingsPanel:
 * - void SetABS(bool value)
 *   Saves the ABS setting.
 *
 * - void SetTC(bool value)
 *   Saves the traction-control setting.
 *
 * ::DropDownMenuButtonSceneChanger:
 * - void OnChange(int index)
 *   Mirrors dropdown selection into the paired button state.
 *
 * - void OnButtonClicked()
 *   Runs the selected scene action.
 *
 * ::SliderScript:
 * - void OnChange(float newValue)
 *   Writes a rounded slider value to the configured TMP label.
 *
 * ----------------------------------------------------------------------
 * @section ui_integration Integration Notes
 *
 * Game data:
 * - ::LevelScrollContent reads GameDataManager.Instance.CurrentGameData.Levels.
 * - ::LevelEntry selects, renames, exports, and removes saved levels.
 * - ::LevelPopUp adds accepted generated maps to saved data.
 * - ::SettingsPanel reads and writes saved assist settings.
 *
 * Generation:
 * - ::GenerateLevelButton uses ::LevelGenerator, ::SeedFactory, and ::LevelCheckPointMaker.
 * - Generated levels are previewed before being saved.
 *
 * Import/export:
 * - ::ImportLevelFromClipboardButton and ::ImportLevelButton use ::ImportExportManager.
 * - ::LevelEntry::ExportLevelButton copies the selected level to the clipboard.
 *
 * Scene management:
 * - ::ChangeSceneButton, ::ReplayButton, ::EditButton, and ::GoPlayButtom route through ::SceneManagement.
 * - ::GoPlayButtom chooses day or night gameplay scene from levelMap::IsDayTrack.
 *
 * Race state:
 * - ::TrackRaceOverlaySource connects live race systems to ::RaceOverlay.
 * - ::ReplayRaceOverlaySource connects replay playback to ::RaceOverlay.
 * - ::RaceOverlay only needs a ::RaceOverlaySource, not direct knowledge of the source system.
 *
 * Notifications:
 * - Import, replay, edit, and custom notification buttons can show feedback through ::NotificationManager.
 *
 * Editor:
 * - ::LevelEditorManager edits a temporary levelMap copy and validates before saving or replacing.
 *
 * ----------------------------------------------------------------------
 * @section ui_performance Performance and GC
 *
 * - Level generation is moved to background tasks by ::GenerateLevelButton.
 * - Level preview buffer construction should not be called every frame.
 * - Texture upload and Unity object access must remain on the Unity main thread.
 * - ::LevelScrollContent rebuilds the full saved-level grid when the game-data hash changes.
 * - Responsive layout work is linear in the number of visible level entries.
 * - ::UIParallax performs one smoothed transform update per configured layer per frame.
 * - Keep parallax layer counts modest.
 * - Avoid generating or previewing very large maps repeatedly from UI events.
 *
 * ----------------------------------------------------------------------
 * @section ui_troubleshooting Troubleshooting
 *
 * Button does nothing:
 * - Check that ::ButtonScript has a concrete ::ButtonType assigned.
 * - Check the Unity Button OnClick event is connected to ::ButtonScript::OnButtonClick.
 * - Check console warnings for missing strategy configuration.
 *
 * Scene button fails:
 * - Check that the target ::SceneAssetHelper is assigned.
 * - Check that the target scene is included in Build Settings.
 * - Check that ::SceneManagement exists.
 *
 * Generated level popup does not open:
 * - Check that ::GenerateLevelButton has popUp assigned.
 * - Check that the popup GameObject has ::LevelPopUp.
 * - Check console output for generation exceptions.
 *
 * Blank preview:
 * - Check that the levelMap is non-null and has positive dimensions.
 * - Check that the RawImage target is assigned in ::LevelPreviewer.
 * - Check that ShowPreviewAsync is called after UI layout exists.
 *
 * Saved level list is empty:
 * - Check GameDataManager.Instance.CurrentGameData.Levels.
 * - Check that generated or imported levels were actually kept/saved.
 * - Check that ::LevelScrollContent is subscribed to GameDataManager changes.
 *
 * Level entry name does not persist:
 * - Check that OnLevelNameChange is connected from the TMP input field.
 * - Check that game data is saved after the change if persistence is required immediately.
 *
 * Overlay does not update:
 * - Check that ::RaceOverlay has an overlay source assigned or discoverable.
 * - For gameplay, use ::TrackRaceOverlaySource.
 * - For replay, use ::ReplayRaceOverlaySource.
 * - Check that the required manager singletons exist.
 *
 * Split popup does not appear:
 * - Check that ::RaceTimeManager calls ::RaceOverlay::DisplaySplit.
 * - Check that checkpoint split UI references are assigned.
 *
 * Settings toggles reset:
 * - Check that ::GameDataManager exists before the panel is enabled.
 * - Check that toggles are connected to ::SettingsPanel::SetABS and ::SettingsPanel::SetTC.
 *
 * Dropdown button crashes or does not update:
 * - Check that dropdown, button text, and scene action arrays are assigned.
 * - Check that the number of scene helpers matches the dropdown options.
 *
 * Notification does not appear:
 * - Check that ::NotificationManager exists.
 * - Check that the notification prefab or popup references are assigned.
 *
 * ----------------------------------------------------------------------
 * @section ui_versions Version History
 *
 * - v1.6: Added RaceOverlaySource abstraction with live gameplay and replay overlay sources.
 * - v1.5: Added settings panel, import/export buttons, replay/edit validation buttons, and day/night play button.
 * - v1.4: Added SliderScript and notification buttons.
 * - v1.3: Added saved-level list UI, LevelEntry, LevelScrollContent, LevelPreviewer, and LevelPopUp.
 * - v1.2: Refactored menu buttons into serialized ButtonType strategies and added GenerateLevelButton.
 * - v1.1: Added RaceOverlay and async preview generation.
 * - v1.0: Added general button script, dropdown scene button, and basic menu UI.
 */

/**
 * @defgroup ui_levels Level UI
 * @ingroup ui
 * @brief Saved-level browser, level-entry cards, and level-list layout UI.
 *
 * @details
 * The Level UI group contains UI components focused specifically on saved custom levels.
 * These components display saved ::levelMap data, best times, day/night icons, preview textures,
 * and list-level actions such as select, rename, export, and remove.
 */