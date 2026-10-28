# NsDepCop configuration

Roslyn analyzer enforcing namespace dependency rules at compile time. Add the `NsDepCop` package to each project and a `config.nsdepcop` beside the project file. Allow-list based: anything not allowed is reported.

```xml
<NsDepCopConfig IsEnabled="true" CodeIssueKind="Error"
                ChildCanDependOnParentImplicitly="true">
  <Allowed From="CorpTraining.Courses.*" To="CorpTraining.Courses.*" />
  <Allowed From="CorpTraining.*" To="CorpTraining.Enrollments.Contracts" />
  <Allowed From="CorpTraining.*" To="CorpTraining.Courses.Contracts" />
  <Allowed From="*" To="CorpTraining.SharedKernel.*" />
  <Allowed From="*" To="System.*" />
</NsDepCopConfig>
```

- `CodeIssueKind="Error"` fails the build. Use `Warning` only during adoption, then ratchet.
- `Namespace.*` matches the namespace and all sub-namespaces.
- A parent-folder `config.nsdepcop` applies to child projects; per-project files refine it.
- Temporary exceptions: an explicit allow rule with a dated comment and ADR or issue link, never deleting rules or disabling the analyzer.
