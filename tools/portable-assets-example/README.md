# Portable asset consumer

Small independent Engine product on the repository's pinned SDK/runtime pair.
It loads a directional sprite atlas and animated GLB through
`PortableAssetContent`, without a Workbench dependency or descriptor parser.

From this repository root, fetch the verified example jobs:

```sh
workbench --workspace /home/agent/dev/asset-pipeline job fetch 68ff501f38ef --to tools/portable-assets-example/content/sprite
workbench --workspace /home/agent/dev/asset-pipeline job fetch 8edb89227534 --to tools/portable-assets-example/content/model
rusty install
rusty build --project tools/portable-assets-example/PortableExample.csproj
```

The example uses the repository's pin in the root `Directory.Build.props`;
`rusty dev --project tools/portable-assets-example/PortableExample.csproj`
runs the staged product on it. Content is
ignored by Git and can be replaced with your own descriptor/member folders.
`PORTABLE_REPORT=/absolute/file.json` writes consumption facts for headless runs.
The verified run reports 32 frames, four directional walk animations and model
clips `idle` and `walk`; normal Engine sprite playback drives the displayed atlas.
