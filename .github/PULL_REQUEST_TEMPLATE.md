<!--
Thanks for the pull request. Keep this short — the diff says what changed,
this section should say why.
-->

## What and why

<!-- What does this change, and what problem does it solve? Link the issue if there is one. -->

Closes #

## How to verify

<!-- What did you run or click to convince yourself this works? -->

## Checklist

- [ ] `dotnet test` passes
- [ ] `npm --prefix web/admin run typecheck` passes
- [ ] Layer rules still hold (`ArchitectureTests` is green)
- [ ] Database changes come with a migration and an updated model snapshot
- [ ] New or non-obvious decisions are explained in comments — why, not what
- [ ] No secrets, tokens or real credentials in the diff
