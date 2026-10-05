# DragonLib.Box2D

DragonLib integration layer for the vendored `Box2D.NET` runtime.

`Box2DWorld` owns the Box2D world handle and guarantees deterministic cleanup. `Box2DConvert` contains explicit conversions between `System.Numerics` and Box2D value types.

The adapter does not hide unit scaling or flip the Y axis. DragonLib uses Y-down game worlds, so a typical downward gravity vector is `new Vector2(0f, 9.81f)`. Keep physics values in game-world units and apply pixel scaling only through `Camera2D.PPU`.

Projects that need physics should reference `DragonLib.Box2D` directly. The core `Engine` project intentionally does not reference Box2D, and `DragonLib.Box2D` does not reference Engine either, so it builds once for desktop and Web.

For the multithreaded solver pass an `IBox2DTaskScheduler` to `new Box2DWorld(gravity, scheduler, workerCount)`. Wrapping Engine's `JobScheduler` takes three lines; see the interface's documentation or `../DragonLib.Tests/Game0/Content/JobSchedulerBox2DTasks.cs`.
