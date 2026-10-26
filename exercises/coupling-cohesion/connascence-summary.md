# Connascence summary

Two components are *connascent* if a change in one requires a change in the other. Use this page to name the kinds of connascence you find in the [exercise](./README.md).

## Static connascence

Visible in the source code. Listed from weakest to strongest.

| Kind | The two sides must agree on | Example |
| --- | --- | --- |
| **Name** | a name | a method or property name |
| **Type** | a type | a parameter's type |
| **Meaning** (convention) | what a value means | `1` means "paid" |
| **Position** | an order | positional arguments |
| **Algorithm** | an algorithm | the same fee formula in two places |

## Dynamic connascence

Visible only at runtime, so it's harder to spot and generally stronger than static connascence. Listed from weakest to strongest.

| Kind | What must line up | Example |
| --- | --- | --- |
| **Execution** | the order of calls | calling `Open()` before `Send()` |
| **Timing** | when things happen | races and timeouts |
| **Values** | several values that must change together | an order total and its line items |
| **Identity** | the instance components refer to | two services that must share one cache object |

## Judging how bad it is

- **Strength:** how hard the coupling is to refactor away. Stronger kinds are lower in the tables above.
- **Locality:** how close the coupled elements are. Strong connascence inside one method is fine; across two services, it's a problem.
- **Degree:** how many elements are affected. Two places that must agree is manageable; forty is not.

> Prefer weaker forms, and keep strong forms close together.
