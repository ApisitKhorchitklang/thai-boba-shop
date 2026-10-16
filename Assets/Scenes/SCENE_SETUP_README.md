Scene wiring and quick setup

Steps to create a working sample scene for the Bubble Tea simulator:

1) Scene objects
- Create an empty GameObject `GameManager` and attach `GameManager` and `InventoryManager`.
- Create an empty GameObject `TimeCyc` and attach the `TimeCyc` script. Assign `timerText` and `day` TMP objects if you have UI TextMeshPro elements.
- Create an empty GameObject `Spawner` and attach `Spawner`. Assign `npcPrefab` (generic pedestrian) and `customerPrefab` (customer prefab with `CustomerController` and `CustomerMovement`). Add several `Spawn Points` transforms and assign them to `spawnPoints`.

2) Counter & ServeStation
- Create a collider (Box) where customers stop (the counter area). Make it `IsTrigger` and attach `ServeStation` to it.
- Ensure your `customerPrefab` has a `Collider` and `CustomerController` component and `CustomerMovement` component. Set `CustomerMovement.target` to a `Transform` near the counter (an empty GameObject placed at counter position).

3) Cup & Player
- Create a `Cup` prefab with `CupController` attached. The player logic to grab/hold the cup is not included; you can create a simple script that instantiates the `Cup` prefab and calls its methods when buttons are pressed.

4) UI Buttons (Canvas)
- Add buttons for: `Order Cups`, `Order Boba`, `Serve`, `End Day`.
- Wire `Order Cups` to call `ComputerTerminal.OrderSupply("cups", amount)` on your `ComputerTerminal` GameObject.
- Wire `Order Boba` to call `ComputerTerminal.OrderSupply("boba", amount)`.
- Wire `Serve` to call `ServeStation.ServeCup` — pass the `CupController` instance (if using UnityEvent with dynamic parameter, you can assign the cup prefab instance).
- Wire `End Day` to `UiEventManagement.EndDayButton()` (also possible: call `GameManager.EndDay()`).

5) Prefabs & References
- Ensure `InventoryManager` exists in the scene (singleton) or create it as a prefab placed in the first scene.
- Assign `TimeCyc` reference in `Spawner` (optional — it auto-finds by script if left empty).

6) Save/Load
- `SaveSystem.SaveGame()` is called by `UiEventManagement.EndDayButton()` and `ComputerTerminal.EndDayAndSave()` to persist inventory and money to JSON at `Application.persistentDataPath`.
- Use `SaveSystem.LoadGame()` in a startup script (e.g., `InventoryManager.Awake()` or a small loader) to restore previous state.

Notes and next steps
- Add proper player grab/hold interaction (touch, mouse, or mobile joystick). The codebase includes several touch/joystick scripts which you can reuse.
- Add visual prefabs for boba/ice/flavors and hook them to `CupController` methods to spawn visuals when ingredients are added.
- Add scoring/tips, order timers, and failure handling for more complex gameplay.
