return args switch
{
    [] => ProjectAssetTests.Run(),
    ["--atlas-authoring", var sdk, var hash, var work] => AtlasAuthoringTests.Run(sdk, hash, work),
    _ => 2,
};
