## What & why

<!-- Short description. Link the upstream .ts/.test.ts if this is a port. -->

## Checklist

- [ ] Upstream reference read (`docs/PORTING.md`) — if this is a port
- [ ] Tests added/ported and passing (`dotnet test -c Release`)
- [ ] `dotnet format --verify-no-changes` clean
- [ ] No floating point introduced in protocol math
- [ ] `docs/PORTING.md` mapping updated (if applicable)
- [ ] CHANGELOG updated (for user-facing changes)
