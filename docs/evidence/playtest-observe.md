# Reading the running product through its playtest commands

> Published copy of a point-in-time live-check record. It is evidence of what was observed then, not a
> statement of current behaviour. LAN addresses are replaced by `<lan-address>`; long hostile and member lists
> are cut to their first entries.

Den task #9004 asked for the Engine's playtest and interaction inspection to be registered, so a live check
reads the place, pose, facing target and hostiles, and each control's key and availability, without the panel.

## What was run

```sh
# stage the operator's imported tables and world plus the hand-written turn scenario (ignored paths);
# bundle.json named mm7-tables, mm7-world, mm7-turn-scenario and was restored afterwards
rusty dev --project src/PartyRpg.Host/PartyRpg.Host.csproj --port 4177 --bind-host <lan-address> --live-debug

# every command below: POST text/plain to http://<lan-address>:4177/__rusty/product/runtime/debug/execute
# input: control/claim with the binding engine.renderer.presentation reported, then input batches of key
# facts in the gameplay.default context (space pressed/released to accept creation, key-w held 0.8 s)
```

## What it read

Before creation was accepted:

```text
> playtest.action party.move-forward
{"id":"party.move-forward","key":"KeyW","durationMs":250,"hold":true,"available":false,"reason":"A party is still being made: the party walks once creation is accepted.",...}
> playtest.look 90 0            [HTTP 422]
steer-creating: A party is still being made: the party walks once creation is accepted.
```

After Space accepted the party:

```text
> playtest.observe
{"mode":"running","admittedSteps":42,...,"steering":{"available":true,...},
 "place":{"id":"57","name":"Barrow III","kind":"interior","open":true},
 "pose":{"x":-415.25250244140625,"y":-1055.604248046875,"z":0.10666656494140625,"yaw":0,"pitch":0,...},
 "movement":{"moved":true,"grounded":true,"blocked":"none",...},
 "facing":{"available":true,...,"reason":"no-candidate",...},
 "combat":{"available":true,"engaged":true,"pacing":"realtime","ready":4,
   "hostile":[{"id":"actor:19","name":"Ghast","distance":403.67,"hitPoints":35,"hitPointsMax":35,"ready":false,"down":false,"activity":"waiting"},...]}, ...}
> playtest.action party.move-forward
{"id":"party.move-forward","key":"KeyW","durationMs":250,"hold":true,"available":true,"reason":null,...}
> playtest.action party.use
{"id":"party.use","key":"KeyG","durationMs":100,"hold":false,"available":false,"reason":"The session would not take 'party.use' while it is running: its controls block does not offer it now.",...}
> interaction.inspect
{"stamp":"57@-415.3,-1055.6,0.1",...,"targetedUseEnabled":false,"selected":null,"totalCandidates":2,
 "candidates":[{"id":44,"label":"A door","distance":404.78,"visibility":"Occluded","yawDeltaDegrees":47.23,...},...]}
```

After `W` was held for 0.8 s, then a look:

```text
> playtest.observe
{..."pose":{"x":-108.05268859863281,"y":-1055.604248046875,"z":0.10666656494140625,"yaw":0,...}, ...}
> playtest.look 90 0
{"yaw":1536,"pitch":0,"lookAdvancesTime":false}
> playtest.look 0 15            [HTTP 422]
look-no-pitch: The party turns but does not look up or down: no control pitches it in play, so a look is a yaw alone.
```

Turning by the yaw `interaction.inspect` reported for door 44 (9.241 degrees from the new pose) left it at
0.0004 degrees: the look and the inspection agree about which way is right.

## What it showed

- `playtest.observe` reported the place and the pose, and the pose moved by about 307 units along x for a
  0.8 s hold of `W`.
- A movement intent is described with its physical key, `KeyW`, as a held control, unavailable with the
  session's reason while creating and available once the party played.
- `interaction.inspect` read the live world's selection, with targeted use off.
