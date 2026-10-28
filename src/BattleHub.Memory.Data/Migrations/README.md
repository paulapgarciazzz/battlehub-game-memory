# Memory migrations

The current migration chain creates each retained table separately:

- `InitialMemoryDatabase`: `GameResults`.
- `ConfigurarGameResultPlayers`: `GameResultPlayers`.
- `AgregarAvisosMatchmaking`: `FinishNotifications` (initially keyed by `MatchId`).
- `FixFinishNotificationPrimaryKey`: changes the notification primary key to `Guid Id` and retains unique `MatchId`.

`AgregarIsDraw` adds a column; it does not create a table. `RemoveUnusedMemoryTables` conditionally drops the legacy `Matches`, `Players`, and `Cards` tables. It is safe on a fresh database, where those tables are never created, and upgrades databases that still have them.

The initial and `ConfigurarGameResultPlayers` operations were adjusted so new databases create one retained table per migration. Existing databases that already applied the old initial migration already have `GameResultPlayers`; the updated configuration migration checks for that table before creating it. Existing databases that applied the old initial migration retain the unused tables until `RemoveUnusedMemoryTables` runs. Back up production data before applying that migration if those legacy tables may contain information that must be retained; this migration intentionally drops them.

The historical migration that created `FinishNotifications` with a text key remains in the chain for upgrade compatibility. `FixFinishNotificationPrimaryKey` corrects the resulting schema before the latest model is reached.

`QuitarAvisosMatchmaking` drops `FinishNotifications`. The game no longer notifies Matchmaking: the microfrontend tells the Shell when a match ends, and the Shell notifies Matchmaking.
