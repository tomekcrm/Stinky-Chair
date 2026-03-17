# Stinky Chair Third-Party Notices

## Cats

Selected parts of the drifter AI organization in `Stinky Chair` are intentionally inspired by the `Cats` mod by `G3rste`.

- Repository: `https://github.com/G3rste/cats`
- Mod ID: `cats`
- Author: `G3rste and contributors`
- License: `MIT`

Reference points inside the `cats` repository:

- custom AI task registration: `src/Cats.cs`
- the `freezeNear` task: `src/AiTasks/TaskFreezeNear.cs`
- drifter patch attaching the `freezeNear` task: `resources/assets/cats/patches/entities/lore/drifter.json`
- equivalent patches for `bowtorn` and `shiver`:
  - `resources/assets/cats/patches/entities/lore/bowtorn.json`
  - `resources/assets/cats/patches/entities/lore/shiver.json`

`Stinky Chair` does not copy cat entities, cat assets, or a runtime dependency on the `cats` mod.
Only the architectural pattern was reused:

- a separate high-priority AI task
- a separate entity state behavior
- a separate asset patch attaching the task to drifters

Implementation inside `Stinky Chair`:

- `src/AI/AiTaskStenchFreezeNearPlayer.cs`
- `src/Behaviors/EntityBehaviorDrifterStenchAI.cs`
- `assets/stench/patches/entities-drifters.json`

Below is the MIT license text as provided by the `cats` repository:

```text
MIT License

Copyright (c) 2021 G3rste and contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
