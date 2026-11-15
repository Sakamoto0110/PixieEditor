return args switch
{
    [] => ProjectAssetTests.Run(),
    ["--atlas-authoring", var sdk, var hash, var work] => AtlasAuthoringTests.Run(sdk, hash, work),
    ["--atlas-animation-playback", var sdk, var hash, var work] => AtlasAuthoringTests.Run(sdk, hash, work, exerciseAnimation: true),
    ["--atlas-animation", var work] => AtlasAnimationTests.Run(work),
    ["--spritesheet-builder", var sdk, var hash, var work] => SpritesheetBuilderTests.Run(sdk, hash, work),
    ["--check-shipping-capture", var work, var capture] => CheckShippingCapture(work, capture),
    _ => 2,
};

static int CheckShippingCapture(string work, string capture)
{
    SpritesheetBuilderTests.CheckSpritePixels(work, capture, false);
    return 0;
}
