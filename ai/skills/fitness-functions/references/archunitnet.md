# ArchUnit.NET module boundary tests

Dedicated test project (e.g. `ArchitectureTests`) referencing every module assembly. Load the architecture once; it is expensive.

```csharp
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

public class ModuleBoundaryTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Courses.CoursesModule).Assembly,
            typeof(Enrollments.EnrollmentsModule).Assembly)
        .Build();

    private static GivenTypesConjunction TypesIn(string ns) =>
        Types().That().ResideInNamespace(ns, useRegularExpressions: false);

    [Fact]
    public void Courses_DoesNotDependOn_EnrollmentsInternals() =>
        TypesIn("CorpTraining.Courses")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("CorpTraining.Enrollments")
                    .And().DoNotResideInNamespace("CorpTraining.Enrollments.Contracts"))
            .Because("modules communicate only through contracts")
            .Check(Architecture);

    [Fact]
    public void Modules_DoNotReachInto_OtherModulesDataAccess() =>
        TypesIn("CorpTraining.Courses")
            .Should().NotDependOnAny(
                Types().That().HaveNameEndingWith("DbContext")
                    .And().DoNotResideInNamespace("CorpTraining.Courses"))
            .Because("each module owns its data")
            .Check(Architecture);

    [Fact]
    public void Contracts_DependOnNo_ModuleImplementation() =>
        TypesIn("CorpTraining.Enrollments.Contracts")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("CorpTraining.Enrollments")
                    .And().DoNotResideInNamespace("CorpTraining.Enrollments.Contracts"))
            .Because("contracts must stand alone")
            .Check(Architecture);
}
```

For every module pair: internals of A must not depend on B outside `B.Contracts`. Generate these from a module list (xUnit `[Theory]` with `MemberData`) once the count grows.
