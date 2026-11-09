# Lighting authoring and debug views

[Documentation index](README.md)

Selecting one entity with `Light2D` in Edit/Select mode shows its
handles: the square on the range ring sets the range, the circle along the light's
direction turns the entity (spot and directional; Shift snaps to 15 degrees), and
the diamond on a spot's cone edge opens or closes the cone. Escape restores the
values and release records one undo step. Light, shadow, occluder and receiver
layer masks show 8 named checkboxes; **Edit light layer names...** names them in the
scene's `LightLayers2D` (names never change masks). **Session > Lighting debug**
draws light bounds and occluder edges over lit scenes, or shows the light buffer or the light
overdraw in place of the frame. Scripts use
`--do lightdrag=<range|direction|cone|cancelrange|canceldirection|cancelcone>:<x>,<y>`,
`lightlayername=<1..8>:<name>` and `lightingdebug=<0..15>`; the development
channel's `world.lighting_debug` reads and sets the debug options.
