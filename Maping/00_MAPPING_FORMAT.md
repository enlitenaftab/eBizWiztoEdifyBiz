# Mapping document format (all modules)

Every `NN_<module>_field_mapping.md` follows the same seven sections, in this order. Nothing is written from column names alone — each row is checked against the client screen, the client DB, our DB, our form, our view and the exe.

---

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | … | … |
| Tables | … | … |
| Code | — | form / view / backend files |
| Exe | — | module number + class |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) | |
| Status | | migrated + verified (latest run) |

## 2. Field mapping

One table per screen section, in the order the client screen shows them.

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|

- **Client UI** — the label exactly as on the eBizWiz screen.
- **Client DB** — `table.column` (and the master it points to).
- **Our DB** — `table.column` the exe writes.
- **Our Form / Our View** — the field label on our add/edit form and on the view page **as saksham sees it**; `—` when saksham does not see it (company-gated or no such field).
- **Rule** — any transformation (lookup, join, default, cap).

## 3. Masters seeded

Master rows the exe creates so the dropdowns show the client's values.

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|

Kind = **New field** · **Label** · **Layout** · **Behaviour** · **Backend**. The section ends with a count of new fields.

### 4b. Database changes (ALTER)

Statements from `00_ALTERS_run_before_exe.sql` that this module needs.

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|

## 6. Verification (latest run)

Counts, totals and record checks against the client.

## 7. Notes

Decisions, reasons and gotchas — always the **last** section.
