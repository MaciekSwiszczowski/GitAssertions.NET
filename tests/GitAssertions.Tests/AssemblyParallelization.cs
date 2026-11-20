using Xunit.Sdk;
using Xunit.v3;

// xUnit's default runs the test classes in parallel but the tests of one class one after another. Mode All runs every
// test at the same time as every other, the tests of one class included. Two reasons:
//  - The suite exercises what the library promises: one GitAssert, which holds a path and no handle, can be shared
//    between tests that run in parallel.
//  - It exposes state the tests share by accident. A test that fails only in parallel is a finding: fix the test or the
//    library; never opt the test out without a stated reason. The one opt-out, with its reason, is
//    NonParallelCollectionForCurrentDirectoryTests.
// https://xunit.net/docs/running-tests-in-parallel
[assembly: Parallelization(Mode = ParallelMode.All)]
