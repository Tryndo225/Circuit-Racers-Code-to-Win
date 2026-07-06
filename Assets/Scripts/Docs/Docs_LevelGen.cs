/**
 * @file Docs_LevelGen.cs
 * @brief Documentation entry for the Level Generation subsystem.
 *
 * @defgroup level_gen Level Generation
 * @ingroup systems
 * @brief Procedural creation, representation, validation, and placement support for grid-based race tracks.
 *
 * @details
 * The Level Generation subsystem contains the data structures and algorithms used to create and validate
 * grid-based race tracks before they are converted into visible scene prefabs.
 *
 * Main components:
 * - ::SeedFactory provides thread-safe integer seeds for System.Random instances.
 * - ::Coordinates represents integer tile-grid coordinates with arithmetic, equality, hashing, and serialization support.
 * - ::LevelMap stores serializable level metadata and the runtime 2D tile grid.
 * - ::LevelGenerator creates point-to-point or circuit layouts by selecting targets, flooding paths,
 *   backtracking roads, and applying spacer tiles.
 * - ::LevelCheckPointMaker converts suitable intermediate straight road segments into checkpoint tiles.
 * - ::LevelMapValidator verifies structural correctness before a map is used or saved.
 * - ::Array2DExtensions provides coordinate-based helpers for rectangular 2D arrays.
 *
 * Tile values are defined by ::LevelMap::LevelTileTypes:
 * - CP = -2: checkpoint tile.
 * - Spacer = -1: keep-out spacer tile.
 * - Grass = 0: empty tile.
 * - Track = 1: road tile.
 * - PlaceHolder = 2: first temporary flood-fill placeholder value; higher values may also be used.
 *
 * Contents:
 * - @ref level_gen_overview
 * - @ref level_gen_data_model
 * - @ref level_gen_tile_model
 * - @ref level_gen_generation
 * - @ref level_gen_validation
 * - @ref level_gen_checkpoints
 * - @ref level_gen_api
 * - @ref level_gen_integration
 * - @ref level_gen_performance
 * - @ref level_gen_troubleshooting
 * - @ref level_gen_versions
 *
 * ----------------------------------------------------------------------
 * @section level_gen_overview Overview
 *
 * Responsibilities:
 * - Produce point-to-point and circuit track layouts on a rectangular grid.
 * - Store track metadata such as name, size, endpoints, lap count, checkpoint count, road coverage,
 *   and day/night variant.
 * - Serialize and deserialize the 2D tile grid through a flattened int array.
 * - Keep generated roads separated with spacer tiles.
 * - Add intermediate checkpoint tiles on long straight segments.
 * - Validate generated or imported maps before use.
 *
 * Threading:
 * - ::SeedFactory::Next is thread-safe.
 * - ::LevelGenerator is CPU-only and does not instantiate Unity scene objects.
 * - ::LevelMap and tile arrays are mutable; the same map instance should not be modified by multiple threads.
 * - Runtime placement and scene integration are Unity-main-thread work handled outside the generator.
 *
 * Important invariants:
 * - A valid map has positive Width and Height.
 * - Tiles has dimensions [Width, Height].
 * - StartPoint and FinishPoint are inside the map.
 * - For circuit maps, FinishPoint matches StartPoint.
 * - For point-to-point maps, StartPoint and FinishPoint are different.
 * - Drivable tiles are Track and CP.
 * - A valid generated road forms one connected track component.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_data_model Data Model
 *
 * ::Coordinates:
 * - X and Y integer components.
 * - Operators:
 *   - addition,
 *   - subtraction,
 *   - multiplication by integer scalar.
 * - Value equality through IEquatable<Coordinates>.
 * - Serializable through ISerializable.
 * - ToString() returns a readable coordinate representation.
 *
 * ::LevelMap:
 * - Name: display name of the level.
 * - Width and Height: grid size in tiles.
 * - Circuit: true for a closed circuit, false for point-to-point.
 * - StartPoint: start tile coordinate.
 * - FinishPoint: finish tile coordinate.
 * - Laps: number of laps used by the level.
 * - CheckpointCountPerLap: number of intermediate checkpoint tiles generated for each lap.
 * - RoadTileCount: number of road tiles recorded by generation.
 * - IsDayTrack: day/night scene or variant flag.
 * - Tiles: runtime int[,] tile grid.
 * - tilesFlat: serialized backing array used because Unity does not serialize rectangular 2D arrays directly.
 *
 * Serialization:
 * - ::LevelMap implements ISerializable.
 * - ::LevelMap implements UnityEngine.ISerializationCallbackReceiver.
 * - OnBeforeSerialize flattens Tiles into tilesFlat.
 * - OnAfterDeserialize rebuilds Tiles from tilesFlat.
 * - GetFlatTiles() stores tiles in row-major order using flatTiles[y * Width + x].
 * - GetUnflattenedTiles(...) rebuilds an int[,] grid from row-major flat data.
 *
 * Copy and equality:
 * - ::LevelMap::Copy creates a new map with copied metadata and copied tile data.
 * - Equality compares metadata and tile contents.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_tile_model Tile Model
 *
 * Coordinate system:
 * - Tiles are addressed as Tiles[x, y].
 * - X represents the column.
 * - Y represents the row.
 * - ::LevelMap::CardinalDirections contains the four-neighbor offsets:
 *   right, left, up, and down.
 *
 * Tile meanings:
 * - ::LevelMap::LevelTileTypes::Grass marks empty cells.
 * - ::LevelMap::LevelTileTypes::Track marks normal road cells.
 * - ::LevelMap::LevelTileTypes::CP marks checkpoint cells.
 * - ::LevelMap::LevelTileTypes::Spacer marks keep-out cells used to discourage cramped roads.
 * - ::LevelMap::LevelTileTypes::PlaceHolder and higher values are temporary flood-fill values.
 *
 * ::Array2DExtensions:
 * - At<T>(T[,] array, Coordinates coords):
 *   Returns a by-reference alias to array[coords.X, coords.Y].
 *
 * - InBounds<T>(T[,] array, int x, int y):
 *   Checks whether the x/y index pair is inside the rectangular array.
 *
 * - Copy<T>(T[,] array):
 *   Creates a new rectangular 2D array with the same element values.
 *
 * - Print(int[,] array):
 *   Formats an integer grid into a simple multiline debug string.
 *
 * - Max<T>(T[,] array):
 *   Finds the maximum element using the default comparer.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_generation Generation Pipeline
 *
 * Public entry points:
 * - ::LevelGenerator::GenerateLevel(int width, int height, bool isCircuit,
 *                                  int steps, int stepLength, int maxAttempts, int seed)
 * - ::LevelGenerator::GenerateLevel(int width, int height, bool isCircuit,
 *                                  int steps, int stepLength, int maxAttempts, System.Random rng)
 *
 * Initialization:
 * - Creates a new ::LevelMap.
 * - Picks a random StartPoint inside the map using internal start padding.
 * - Sets Width, Height, Circuit, Tiles, and IsDayTrack.
 * - Initializes all cells to Grass.
 * - Marks the StartPoint as Track.
 * - Calls the internal TrackStarter step to open the beginning of the generated road.
 *
 * Iterative carving:
 * - The generator repeats up to steps times.
 * - TryStep attempts one extension from the current road end.
 * - PickTarget selects a candidate coordinate near the previous valid point.
 * - FloodingAlgorithm searches through available cells and writes temporary placeholder values.
 * - BackTrack follows the placeholder values back and converts the chosen path into Track tiles.
 * - RoadSpacer writes Spacer tiles around road nodes when needed to reduce cramped layouts.
 * - RemovePlaceholders clears leftover temporary flood-fill values back to Grass.
 *
 * Finishing:
 * - For circuit maps, CircuitFinisher attempts to connect the generated road back to StartPoint
 *   and FinishPoint is set to StartPoint.
 * - For point-to-point maps, FinishPoint is set to the last successfully carved point.
 *
 * Coverage retry:
 * - If RoadTileCount is below the required coverage threshold, generation retries using the same
 *   Random source.
 *
 * Road spacing:
 * - RoadSpacer checks cardinal neighbors of a road tile.
 * - If the tile has more than one adjacent road neighbor, nearby empty or placeholder cells may be
 *   converted to Spacer.
 * - Spacer cells are not drivable and are used only during layout generation.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_validation Validation
 *
 * ::LevelMapValidator validates structural correctness of a ::LevelMap.
 *
 * A valid map must:
 * - Be non-null.
 * - Have a non-null Tiles grid.
 * - Have positive Width and Height.
 * - Have Tiles dimensions matching Width and Height.
 * - Have StartPoint and FinishPoint inside the map.
 * - Have start and finish on track-compatible tiles.
 * - Use StartPoint == FinishPoint for circuit maps.
 * - Use StartPoint != FinishPoint for point-to-point maps.
 * - Contain at least one track-compatible tile.
 * - Contain exactly one connected track component.
 * - Use correct endpoint rules.
 * - Use correct neighbor counts.
 * - Place checkpoint tiles only on straight sections.
 *
 * Track-compatible tiles:
 * - ::LevelMap::LevelTileTypes::Track.
 * - ::LevelMap::LevelTileTypes::CP.
 *
 * Circuit rules:
 * - StartPoint and FinishPoint must be the same tile.
 * - Every track-compatible tile must have exactly two track-compatible neighbors.
 * - Checkpoint, start, and finish tiles must have opposite track neighbors, meaning they sit on a straight section.
 *
 * Point-to-point rules:
 * - StartPoint and FinishPoint must be different.
 * - StartPoint and FinishPoint must each have exactly one track-compatible neighbor.
 * - All other track-compatible tiles must have exactly two track-compatible neighbors.
 * - Checkpoint tiles must have opposite track neighbors, meaning they sit on a straight section.
 *
 * Connectivity:
 * - Validation counts all track-compatible tiles.
 * - It flood-fills from the first track-compatible tile found.
 * - The map is valid only if the connected count equals the total track-compatible tile count.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_checkpoints Checkpoint Stamping
 *
 * ::LevelCheckPointMaker::GenerateCheckPoints converts suitable intermediate straight road cells into CP tiles.
 *
 * Purpose:
 * - Add intermediate checkpoint tiles after the basic road layout has been generated.
 * - Keep start and finish cells unchanged.
 * - Increment LevelMap::CheckpointCountPerLap for each generated checkpoint.
 *
 * Behaviour:
 * - Traversal starts from LevelMap::StartPoint.
 * - It follows connected Track tiles using four-neighbor movement.
 * - It avoids immediately stepping back to the previous tile.
 * - It does not step onto LevelMap::FinishPoint.
 * - It tracks direction changes to detect straight segments.
 * - When a straight segment is long enough, a checkpoint is placed near the middle.
 *
 * Design note:
 * - This utility is intended for intermediate checkpoints.
 * - The start and finish blocks already provide their own checkpoint trigger in the placed track scene.
 * - Therefore the generator does not need to create extra checkpoint tiles on the start or finish cells.
 *
 * Public entry point:
 * - ::LevelCheckPointMaker::GenerateCheckPoints(LevelMap levelMap,
 *                                               int minStraightLengthForCheckPoint = 4)
 *
 * Preconditions:
 * - levelMap is non-null.
 * - levelMap.Tiles is initialized.
 * - Road cells are marked as ::LevelMap::LevelTileTypes::Track.
 *
 * Effects:
 * - Some Track cells may become CP cells.
 * - CheckpointCountPerLap increases for each generated checkpoint.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_api Public API Reference
 *
 * ::SeedFactory:
 * - static int Next()
 *   Returns a new integer seed by atomically advancing the internal seed value.
 *
 * ::Coordinates:
 * - Coordinates(int x, int y)
 *   Creates a coordinate from integer components.
 *
 * - Coordinates(SerializationInfo info, StreamingContext context)
 *   Deserializes a coordinate.
 *
 * - static Coordinates operator +(Coordinates a, Coordinates b)
 *   Adds two coordinates component-wise.
 *
 * - static Coordinates operator -(Coordinates a, Coordinates b)
 *   Subtracts two coordinates component-wise.
 *
 * - static Coordinates operator *(Coordinates a, int b)
 *   Multiplies a coordinate by an integer scalar.
 *
 * - static Coordinates operator *(int a, Coordinates b)
 *   Multiplies a coordinate by an integer scalar.
 *
 * - bool Equals(Coordinates other)
 *   Checks value equality.
 *
 * - void GetObjectData(SerializationInfo info, StreamingContext context)
 *   Writes coordinate serialization data.
 *
 * ::LevelMap:
 * - LevelMap()
 *   Creates a default empty map.
 *
 * - LevelMap(SerializationInfo info, StreamingContext context)
 *   Reconstructs a map from serialized data.
 *
 * - void GetObjectData(SerializationInfo info, StreamingContext context)
 *   Writes serialized map data.
 *
 * - int[] GetFlatTiles()
 *   Returns a row-major flattened copy of Tiles.
 *
 * - static int[,] GetUnflattenedTiles(int[] flatTiles, int height, int width)
 *   Rebuilds a rectangular tile grid from flattened row-major data.
 *
 * - void OnBeforeSerialize()
 *   Updates the flattened serialized tile array.
 *
 * - void OnAfterDeserialize()
 *   Rebuilds the runtime tile grid.
 *
 * - LevelMap Copy()
 *   Returns a copied level map.
 *
 * - string ToString()
 *   Returns a multiline representation of the tile grid.
 *
 * ::LevelGenerator:
 * - static LevelMap GenerateLevel(int width, int height, bool isCircuit,
 *                                 int steps, int stepLength, int maxAttempts, int seed)
 *   Generates a level from an explicit seed.
 *
 * - static LevelMap GenerateLevel(int width, int height, bool isCircuit,
 *                                 int steps, int stepLength, int maxAttempts, System.Random rng)
 *   Generates a level using a provided random source.
 *
 * ::LevelCheckPointMaker:
 * - static void GenerateCheckPoints(LevelMap levelMap, int minStraightLengthForCheckPoint = 4)
 *   Adds intermediate checkpoint tiles to long straight road segments.
 *
 * ::LevelMapValidator:
 * - static bool Validate(LevelMap lvl)
 *   Returns true when the map satisfies dimension, endpoint, connectivity, neighbor-count, and checkpoint rules.
 *
 * ::Array2DExtensions:
 * - static ref T At<T>(this T[,] array, Coordinates coords)
 *   Returns a by-reference element at the given coordinate.
 *
 * - static bool InBounds<T>(this T[,] array, int x, int y)
 *   Checks array bounds.
 *
 * - static T[,] Copy<T>(this T[,] array)
 *   Copies a rectangular 2D array.
 *
 * - static string Print(this int[,] array)
 *   Formats an integer grid for debugging.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_integration Integration Notes
 *
 * Track placement:
 * - ::RaceTrackPlacer consumes a ::LevelMap and instantiates visible track-piece prefabs.
 * - CP tiles are converted into checkpoint prefabs with ::CheckPointListener components.
 * - The placed start/finish object is used as the car spawn reference.
 *
 * Race flow:
 * - ::CheckPointManager discovers and orders placed checkpoint listeners.
 * - ::TrackManager starts the race, activates checkpoints, tracks laps/checkpoints, and handles respawn.
 *
 * Game data:
 * - ::GameDataManager stores custom ::LevelMap instances inside saved game data.
 * - Edited or imported maps should be validated before being stored.
 *
 * Import/export:
 * - ::ImportExportManager can encode and decode ::LevelMap data for sharing.
 * - Decoded maps should pass ::LevelMapValidator::Validate before being accepted.
 *
 * UI:
 * - Level editor UI can modify a copied ::LevelMap.
 * - Level preview UI can render Tiles directly or through generated preview textures.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_performance Performance and GC
 *
 * Complexity:
 * - Flood-fill and backtracking are linear in the explored grid area.
 * - Worst-case generation work is proportional to Width * Height per attempted path search.
 * - Validation is also linear in the number of tiles.
 * - Checkpoint stamping is linear in the traversed road length.
 *
 * Allocation notes:
 * - Generation creates lists for modified flood-fill positions.
 * - LevelMap::Copy duplicates the tile grid.
 * - GetFlatTiles allocates a new one-dimensional array.
 * - Debug Print output allocates strings and should not be used in performance-sensitive paths.
 *
 * Tuning:
 * - Larger Width and Height increase flood-fill cost.
 * - Higher steps and maxAttempts increase generation time.
 * - Larger stepLength creates longer candidate jumps and may increase path search work.
 * - Very strict spacing can make generation retry more often.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_troubleshooting Troubleshooting
 *
 * Generation retries too often:
 * - Increase steps or maxAttempts.
 * - Reduce spacing strictness if applicable.
 * - Use a larger map.
 * - Check whether the road coverage threshold is too difficult for the chosen parameters.
 *
 * Circuit fails validation:
 * - Ensure FinishPoint equals StartPoint.
 * - Ensure every track-compatible tile has exactly two neighbors.
 * - Ensure start/finish sits on a straight section when required.
 *
 * Point-to-point map fails validation:
 * - Ensure StartPoint and FinishPoint are different.
 * - Ensure only start and finish have one neighbor.
 * - Ensure all intermediate track-compatible tiles have two neighbors.
 *
 * Checkpoints are missing:
 * - Lower minStraightLengthForCheckPoint.
 * - Ensure the generated road contains long enough straight segments.
 * - Remember that start and finish checkpoint triggers are provided by placed start/finish prefabs.
 *
 * Checkpoint validation fails:
 * - Checkpoint tiles must sit on straight sections.
 * - A checkpoint should have opposite track-compatible neighbors.
 *
 * Road looks too cramped:
 * - Increase map size.
 * - Tune stepLength and maxAttempts.
 * - Review spacer placement around multi-neighbor road tiles.
 *
 * Imported map fails:
 * - Check tile dimensions against Width and Height.
 * - Check that start and finish are inside bounds.
 * - Check that tile values use known LevelTileTypes.
 * - Run ::LevelMapValidator::Validate before saving or playing.
 *
 * ----------------------------------------------------------------------
 * @section level_gen_versions Version History
 *
 * - v1.4: Added LevelMapValidator documentation and clarified endpoint, neighbor-count, and checkpoint rules.
 * - v1.3: Added IsDayTrack, checkpoint count, road count, copy/equality, and flattened serialization details.
 * - v1.2: Added straight-segment checkpoint stamping for intermediate checkpoints.
 * - v1.1: Added circuit finishing and road spacer refinement.
 * - v1.0: Initial flood-fill/backtrack generator with serializable LevelMap.
 */