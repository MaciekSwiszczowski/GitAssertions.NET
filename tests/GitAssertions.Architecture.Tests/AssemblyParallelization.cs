using Xunit.Sdk;
using Xunit.v3;

// xUnit's default runs the test classes in parallel but the tests of one class one after another. Mode All runs every
// test at the same time as every other, the tests of one class included, here the checks that read this repository.
// They only read it, and one GitAssert, which holds a path and no handle, is what the library promises to share between
// tests that run in parallel. A test that fails only in parallel is a finding: fix the test or the library; never opt
// the test out without a stated reason.
// https://xunit.net/docs/running-tests-in-parallel
[assembly: Parallelization(Mode = ParallelMode.All)]
