// The fixtures of the full-system tests run two at a time: each test has its own browser and its own page, the site
// is one process that only serves files, and nothing is shared between tests but that. The build has thirty minutes
// (the workflow says so), and the garden is drawn in software there. A test that measures time runs alone
// (NonParallelizable).
[assembly: NUnit.Framework.Parallelizable(NUnit.Framework.ParallelScope.Fixtures)]
[assembly: NUnit.Framework.LevelOfParallelism(2)]
