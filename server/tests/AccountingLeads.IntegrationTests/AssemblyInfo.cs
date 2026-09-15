using Xunit;

// All test classes in this assembly share one physical LocalDB database
// (AccountingLeadsDb_IntegrationTests, see IntegrationTestWebApplicationFactory) and reset it
// before each test runs. xUnit runs distinct test classes in parallel by default (each gets
// its own implicit collection), which would let one class's database reset/writes race with
// another class's in-flight assertions against the same shared database. Disabling
// parallelization for this assembly keeps integration tests correct without introducing
// per-test transactional isolation or a database-per-class scheme.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
