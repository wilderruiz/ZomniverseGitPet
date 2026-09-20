# Guardian V3 scaffold

Guardian V2 is the current working live GDI+ vector renderer and remains the Lynx Lab default and fallback.

Guardian V3 is an experimental layered vector/SVG renderer. This first milestone only proves runtime renderer selection and shared state propagation; it does not contain final mascot artwork.

The intended movable layer set is:

- head
- ears
- eyes
- muzzle
- chest
- body
- legs
- tail
- collar
- shield

The drawing backend remains behind `ILynxRenderer`. A later Direct2D/DirectComposition implementation can replace the V3 backend without changing the Lynx Lab controls or V2 renderer.

The `Assets` tree reserves locations for future approved layered artwork. No production SVG artwork is included yet.
