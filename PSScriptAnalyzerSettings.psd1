@{
    # A warning is an error: the build fails on any finding of these severities.
    Severity     = @('Error', 'Warning')
    ExcludeRules = @(
        # The log of a build or a deployment step is its standard output: "==> step", PASS, FAIL, SKIP.
        'PSAvoidUsingWriteHost',
        # The steps of build.ps1 are named as the bootcamp's are: Init, Compile, UnitTests, Publish.
        'PSUseApprovedVerbs',
        # Start-Site, Stop-Site and the like are steps of a script, not cmdlets a person runs with -WhatIf.
        'PSUseShouldProcessForStateChangingFunctions'
    )
}
