We added a few lines to FinalIK package (version 1.9) to suit our needs. Specifically, null checking conditions are added to
`Assets/Plugins/RootMotion/FinalIK/InteractionSystem/InteractionObject.cs`.

Initiate method at line 320 of InteractionObject.cs is updated as follows:

* For loop body starting at line 322 is enclosed by the if condition checking "weightCurves" is not null:

```csharp
if (weightCurves != null) {
    for (int i = 0; i < weightCurves.Length; i++) {
        if (weightCurves[i].curve.length > 0) {
            float l = weightCurves[i].curve.keys[weightCurves[i].curve.length - 1].time;
            length = Mathf.Clamp(length, l, length);
        }
    }
}
```

* For loop starting at line 330 is enclosed by the similar if condition:


```csharp
if (events != null) {
    for (int i = 0; i < events.Length; i++) {
        length = Mathf.Clamp(length, events[i].time, length);
    }
}
```
