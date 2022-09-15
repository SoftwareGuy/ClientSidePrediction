# Upgrade from 1.0.0-beta.4 to 1.0.0-beta.5

Please read atleast the headers for each change. Some will not give compile errors but will break your game

### PredictionManager (changed)

PredictionManager can now stop and start the simulation client and server

- `SetClientReady(true)` will run the simulation on client, and tell tell the server to send world snapshots
- `SetServeRunning(true)` will run the simulation on server

both these values start as false, and should be set to true when the game is ready to run. For example after all clients have loaded the scene.

#### After
```cs
ClientManager.SetClientReady(true);
ServerManager.SetServerRunning(true);
```


### Apply Inputs (changed)

#### Before
```cs
public override void ApplyInputs(InputState input, InputState previous)
{
    var move = input.Horizontal * new Vector3(1, .25f /*small up force so it can move along floor*/, 0);
    body.AddForce(speed * move, ForceMode.Acceleration);
    if (input.jump && !previous.jump)
    {
        body.AddForce(Vector3.up * 10, ForceMode.Impulse);
    }
}
```

#### After
```cs
public override void ApplyInputs(NetworkInputs<InputState> inputs)
{
    var previous = inputs.Previous;
    var current = inputs.Current;

    var move = current.Horizontal * new Vector3(1, .25f /*small up force so it can move along floor*/, 0);
    body.AddForce(speed * move, ForceMode.Acceleration);
    if (current.jump && !previous.jump)
    {
        body.AddForce(Vector3.up * 10, ForceMode.Impulse);
    }
}
```



### ApplyState (changed)

#### Before 
```cs
public override void ApplyState(ObjectState state)
{
    body.position = state.position;
    body.velocity = state.velocity;
    body.rotation = Quaternion.identity;
    body.angularVelocity = Vector3.zero;
}
```

```cs
public override void AfterStateChanged()
{
    body.position = state.position;
    body.velocity = state.velocity;
    body.rotation = Quaternion.identity;
    body.angularVelocity = Vector3.zero;
}
```

### State Object (changed)

State has been moved from a struct to unmanaged pointers.

Because of this the struct used must use explicit layout with a size. This size will be rounded up to the nearest 32 bits for memory allocations

#### Before
```cs
[NetworkMessage]
public struct ObjectState
{
    public readonly bool Valid;
    public readonly Vector3 position;
    public readonly Vector3 velocity;

    public ObjectState(Vector3 position, Vector3 velocity)
    {
        this.position = position;
        this.velocity = velocity;
        Valid = true;
    }
}
```
#### After
```cs
[StructLayout(LayoutKind.Explicit, Size = 25)]
public struct ObjectState
{
    [FieldOffset(0)] public readonly Vector3 position;
    [FieldOffset(12)] public readonly Vector3 velocity;
    [FieldOffset(24)] public readonly NetworkBool Valid;

    public ObjectState(Vector3 position, Vector3 velocity)
    {
        this.position = position;
        this.velocity = velocity;
        Valid = true;
    }
}
```




### NetworkBool (added)

Boolean values may have different representations on different computers, so there is a struct that stores a Boolean value as a byte instead.

Note: Current this is only needed for state. Input structs are still serialized using network writer.

#### Before
```cs
public bool Trigger;
```
#### After
```cs
public NetworkBool Trigger;
```

### GatherState (removed)

Now reads state from unmanaged pointer directly.

The `State` property returns the value as `ref` so it can be modified without having to set the value again

`State` should be modified within `NetworkFixedUpdate`

if non-network state is update by `Physics.simulate` then you can use `AfterTick` to update the network state object

#### Before
```cs
public override ObjectState GatherState()
{
    return new ObjectState(body.position, body.velocity);
}
```
#### After
```cs
public override void AfterTick()
{
    State.position = body.position;
    State.velocity = body.velocity;
}
```


### ResimulationTransition (changed)

`ResimulationTransition` should now return the new state, and it will be applied internally after resimulation is finished

note: this function is `virtual`, so for no smoothing just remove the `override`

#### Before
```cs
public override void ResimulationTransition(ObjectState current, ObjectState next)
{
    // apply smoothing here
}
```
#### After
```cs
public override ObjectState ResimulationTransition(ObjectState current, ObjectState next)
{
    // apply smoothing here
    return ...;
}
```

