# 14. Sales Target · Campaign · Expenses · Task — field mapping

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | Marketing & Target → Sales Target on User; Marketing & Target → Campaign Entry; Master → Expenses; Task (task entry) | Target; Mass Mail (campaign); Expense; Task |
| Tables | Sales Target: `trhtargt` (89) + periods `trdtargt1period` (239) + principal lines `trdtargt2items` (205) + actions `trdtargt3actions` (0). Campaign: `trhcampa` (8) + cities `trdcampa1geogr` (3) + items `trdcampa3prods` (4) + party profiles `trdcampa4pprof` (2) + inquiry-source modes `trdcampa5modes` (4) + expense `trdcampa6expense` (0). Expenses: `trhexpense` (3) / `trdexpense1detail` (2) + `mstexpensetype`. Task: `trdtask` (5) + `mstactiontobetaken`, `mstactiontaken`, `mstfixedselection` | `target`; `mmcampaign`; `expense` / `expensedetails` / `expensestatus` + `ExpCategory`; `task` + `ticket` + `nextaction` |
| Code | — | Target: `target/default.asp`, `app/target.asp`, `assets/scripts/target.js`. Mass Mail: `massmail/default.asp`, `app/massmail.asp`. Expense: `expense/default.asp`, `expense/app/expense.asp`, `expense/script/expense.js`. Task: `task/default.asp`, `app/task.asp`, `assets/scripts/task.js` |
| Exe | — | **14. Sales Target (User)** `SalesTarget`; **15. Campaign (Mass Mail)** `Campaign`; **16. Expenses** `ExpenseEntry`; **17. Task** `TaskEntry` |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) | |
| Status | | migrated + verified (latest Run ALL, 0 errors) |

- **Sales Target on User** — every one of the 239 periods is a full year (12 months starting on the 1st, e.g. 01/04/2026 – 31/03/2027, ₹3,00,00,000 for Akash Chanda); total ₹641.80 Cr. 87 distinct users, 87 / 87 match a migrated user by name. One overlap: Manish Saini, FY from 01/04/2017, has two periods in two different target headers (₹1 Cr + ₹2 Cr). 14 targets split their year by principal (205 lines, each with quantity and amount; `nitemcategory` is empty on all of them).
- **Campaign** — CA1 "aa" (2016), CA2 "Esco Laboratory Shakers…" (Information Sharing), CA3 "Wish You A Very Happy Diwali", CA4 "Happy Deepavali" (Customer Greetings), CA5 "Bruker Bravo Handheld Raman Spectro…" (Information Sharing), CA6 "Test CAmpaign", CA7 "test", CA8 "Mails : Sales Team" (2021). The four real campaigns (CA2–CA5) have mode **"Mass Mailing"** in `trdcampa5modes`. Expected sales is empty on all 8.
- **Expenses** — all numbered `UE1` (one per office). Office 2 (08/07/2024) and office 3 (03/02/2024) have no line and no amount; office 6 (17/05/2017) "expense of 17.05.2017 --- test" has 2 lines: travelling ₹1,000 and food allowance ₹250.
- **Task** — 5 task-entry rows.

EdifyBiz keeps a target as **one row per executive + module + month** (`targetdate` = 1st of the month); the bulk form builds a grid of the months of a financial (Apr–Mar) or calendar year and loads the existing row with `where executive = … and module = … and targetdate = … order by code desc` (`app/target.asp`). EdifyBiz has no marketing-campaign module; its only campaign is **Mass Mail** (`mmcampaign`), which the migrated campaigns use as header records.

---

## 2. Field mapping

### 2.1 Sales Target on User → `target` (module `SAL`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | User | `trhtargt.nuser` | `target.executive` | Users (row of the target grid) | Executive (list filter) | user map by name (87 / 87) |
| 2 | — | — | `target.module` | Module = Sales With Amount | Module (list filter) | always **`SAL`** |
| 3 | Period From / To | `trdtargt1period.dfromdate` … `dtodate` | `target.targetdate` | month column of the grid (e.g. April/2026) | Target Date (e.g. April 2026) | **12 rows** per yearly period, the 1st of each month |
| 4 | Amount | `trdtargt1period.namount` | `target.tamt` | amount cell of the month | Target Amount | `namount / 12` rounded to 2 decimals; the last month takes the rounding difference so the 12 rows add up to the client's yearly figure exactly |
| 5 | Trn. No. | `vtrnprefix` + `ntrnno` | `target.remark` | — | — | header `Sales Target ST38 \| yearly 30000000.00 (01/04/2026-31/03/2027)` on every month; two periods of the same user + year are summed into one year and both numbers are named with their amounts (`ST… (…) + ST… (…)`) |
| 6 | Ref No / Ref Date | `vrefno`, `drefdate` | `target.remark` | — | — | `\| Ref No: … dt. dd/MM/yyyy` after the header |
| 7 | Remarks | `vremarks` | `target.remark` | — | — | appended after the header |
| 8 | Principal split (item grid) | `trdtargt2items.nprincipal`, `nquantity`, `namount` | `target.remark` | — | — | `\| Principal split: <principal>: Qty n, amount;` — the lines fill the 500-character remark month after month until all are written; nothing is cut |
| 9 | — | `addedon` / `editedon` | `createdon` / `updatedon` | — | — | `createdby` / `updatedby` = migration user |
| 10 | — | — | `ccode`, `scode`, `pcode` | Customer / Supplier / Product Name | Customer / Supplier / Product Name ("All …") | left empty (see Notes) |

The remark is stored in `target.remark` and returned by `viewtarget` (`app/target.asp`); the Target screen has no Remark field.

### 2.2 Campaign Entry → Mass Mail `mmcampaign`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Campaign Name | `trhcampa.vcampaignname` | `mmcampaign.title` | Title | Title | capped at 100; falls back to the campaign number |
| 2 | Objective | `vobjective` ("Information Sharing", "Customer Greetings" …) | `mmcampaign.subject` | Subject | Subject | the form requires a subject; falls back to the name |
| 3 | From Date | `dfromdate` | `mmcampaign.scheduleddate` | Schedule Date and Time | Schedule Date & Time | `dtrndate` when empty |
| 4 | — | — | `mmcampaign.sent` | — | Status / list column Sent | **2 = Sent** (`MassMailStatusLabel`: 0 Pending, 1 In Process, 2 Sent) |
| 5 | — | — | `countsent`, `countopened`, `countbounced`, `countrejected`, `countspam` | — | Total E-Mail Sent / Opened / Bounced / Rejected, Spam | 0 — no recipient is loaded |
| 6 | Trn. No. / Trn. Date | `vtrnprefix` + `ntrnno`, `dtrndate` | `mmcampaign.body` | Message Body | Message Body tab | first line `Migrated from eBizWiz Campaign CA… (dd/MM/yyyy)` |
| 7 | Campaign Name, Period, Objective, Expected Sales | `vcampaignname`, `dfromdate` / `dtodate`, `vobjective`, `nexpectedsales` | `mmcampaign.body` | Message Body | Message Body tab | one HTML line each (`<br/>`), text HTML-encoded; Expected Sales only when not 0 |
| 8 | Promotions and Budget (mode / budget) | `trdcampa5modes.ninquirysource` → `mstinquirysource`, `nbudget` | `mmcampaign.body` | Message Body | Message Body tab | `Mode / Budget: <source> (budget n); …` |
| 9 | Campaign Details: Cities / Party Profiles / Items | `trdcampa1geogr.ncity`, `trdcampa4pprof.npartyprofile`, `trdcampa3prods.nitem` | `mmcampaign.body` | Message Body | Message Body tab | `Cities:`, `Party Profiles:`, `Items:` lines |
| 10 | Ref No / Date, Remarks, Comment | `vrefno`, `drefdate`, `vremarks`, `vcomment` | `mmcampaign.body` | Message Body | Message Body tab | one line each when filled |
| 11 | Approval | `bapproval`, `approvedon` | `mmcampaign.body` | Message Body | Message Body tab | `Approved: Yes on dd/MM/yyyy` / `No` |
| 12 | — | `addedon` / `editedon` | `createdon` / `updatedon` | — | — | `createdby` / `updatedby` = migration user |

Only the campaign header is written — no `mmcampaignemails` recipient and no `mmemailtempaltemapping` template mapping.

### 2.3 Master > Expenses → `expense` / `expensedetails`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `vtrnprefix` + `ntrnno` (UE1) | `expense.expenseno` | — (Expense Number is gated to Zinq / scanera / Skytech …) | — | kept as the key |
| 2 | Trn. Date | `dtrndate` | `expense.date` | Date | Date | |
| 3 | User | `nuser` | `expense.executive` | Employee | Employee | user map; `addedby` when empty, else migration user |
| 4 | — | — | `expense.currency` | Currency | Currency | INR |
| 5 | Approval | `bapproval` | `expense.status` 'Sanctioned', `expenselevel` 2, `statusupdatedby` / `statusupdatedon` + one `expensestatus` row ("Approved in eBizWiz") | — | Status; Status tab (Date / Status / Remark / Created By) | not approved → NULL ("Not Entered") |
| 6 | Remarks (header) | `trhexpense.vremarks` | first line `expensedetails.remark` "… \| Expense remark: …" | Narration | Expense Details tab | the header has no remark column |
| 7 | Expense Type | `trdexpense1detail.nexpensetype` → `mstexpensetype` | `expensedetails.expensetype` → `ExpCategory` | Expense Type | Expense Details tab | master seeded by name (§3) |
| 8 | Date / Amount / Remarks | `dexpdate`, `namount`, `vremarks` | `expensedetails.date`, `amount` = `requestedamount`, `remark` | Date / Amount / Narration | Expense Details tab | as `addexpensedetails` writes them; line date falls back to the header date |
| 9 | — | `addedon` / `editedon` | `createdon` / `updatedon` | — | — | `createdby` / `updatedby` = migration user |

Approver 1 / 2 stay empty (eBizWiz has no approver). The expense total is the sum of its lines; the app stores no header amount.

### 2.4 Task (`trdtask`) → `task` + `ticket`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Action To Be Taken | `nactiontobetaken` → `mstactiontobetaken` | `task.title` | Title | page heading of the task | e.g. "Call For Follow Up"; "Task" when empty |
| 2 | Action To Be Taken | same | `task.nextaction` (text) + `nextaction` master (module Task) | Next Action | Next Action | names not yet in the master are seeded (§3) |
| 3 | Task Date | `dtaskdate` | `task.duedate`, `task.startdt` | Due Date | Due Date / Start Date | |
| 4 | Priority | `npriority` → `mstfixedselection` (HIGH / MEDIUM) | `task.priority` | Priority | Priority | by name → High / Medium |
| 5 | Task Added By | `ntaskaddedby` | `task.assignedto` | Assigned To | Notified To | the only user on the row; migration user when not mapped |
| 6 | Task Completed | `btaskcompleted` | `task.status` | Status | Status | 1 → the Task status with behavior Completed (Close), else Open |
| 7 | Remarks | `vremarks` | `task.remark` | Notes | Description | |
| 8 | Action Taken / Action Taken Date | `nactiontaken` → `mstactiontaken`, `dactiontakendate` | `task.remark2` "Action Taken: … on dd/MM/yyyy HH:mm" | — | — | the Remark field for `remark2` is Spectrum-only |
| 9 | — | — | `important`, `urgent`, `isticket`, `escalationmatrix`, `isAcknowledged` | — | — | 0 |
| 10 | — | — | `ticket` (`mcode` = task, category `TSK`, remark "Title : … / Remark : …", status, priority, assignedto, duedate, nextaction) | — | — | the same row `TaskCreationTicket` writes for a new task |
| 11 | — | `addedon` / `editedon` | `createdon` / `updatedon` | — | — | `createdby` / `updatedby` = migration user |

---

## 3. Masters seeded

| Master | Rows | Rule |
|---|---|---|
| `ExpCategory` (Expense Type) | 3 — Expense, UTD, BY ROAD | the full eBizWiz `mstexpensetype` list, by name; existing names reused |
| `nextaction` (module Task) | the Action To Be Taken names not yet in the master | by name, `makedefault` 0 |

Target and Campaign need no master.

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| — | Target, Mass Mail, Expense, Task | none — the standard screens are used as they are; Target already offers "Sales With Amount" | — | — | — |

New fields: 0

### 4b. Database changes (ALTER)

None.

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Marketing & Target > Sales Target on Office | `trhtargtoffice` / `trdtargtoffice1period` / `trdtargtoffice2items` | 7 headers / 20 yearly periods / 46 item rows (2012–2018, SALES / SERVICE TARGET) | no office target in our standard: `target` insert / edit writes only `executive, targetdate, tamt, module, remark, ccode, scode, pcode` (`app/target.asp`), every reader filters by `executive` + `module` (`app/reports/target.asp`, `app/include.asp`), there is no branch column and no office-target table; putting the figure on a user would invent an owner and double the user targets |
| Sales Target > Actions | `trdtargt3actions` | 0 | no data |
| Campaign Entry > expense | `trdcampa6expense` | 0 | no data |
| Service > Claim Form Entry | `trhclaim` / `trdclaim1items` | 0 / 0 | no data in offices 2/3/4/6 (the 3 claims in the DB belong to the WinMax test offices) |
| Upload Doc / DMS (every screen) | `trhdoc` | 284,599 file references | skipped by the user; the files live on the client server |
| Inquiry > Cold Call / Tour Plan | — | — | no table of its own; it creates Inquiry Entry rows (migrated in 03) |
| Assign Service Routes | `msduserroute`, call routes | 0 | no data (party routes → contact Area, 01) |
| Write Off Missing Quantity | write-off rows | 0 | no data |
| Physical Stock Taking / user stock transfer | headers | 5 / 1, 0 lines | nothing to post (09) |
| Quotation Technical Set | `trdquote4tech` | 3 | every value column empty |
| Reschedule PM Visits, Contract Sign-Up / Renewal Advice, Schedule Bills Generate / Print, Range Prints, Call Sheets, Owner / Action Re-Allocation, Call Allocation, Search Serial Number, Global Pending Calls | — | — | action / print / report screens with no data of their own; they act on migrated documents |

## 6. Verification (latest run)

### Sales Target (module 14)

- `target` **2,856** rows (239 periods × 12 = 2,868, less 12 for the merged Manish Saini year), `module = 'SAL'`, total **₹6,41,80,07,001.00** = client.
- Per user + year: **238 / 238** yearly totals match eBizWiz exactly (the 12 monthly rows add up to the client's figure).
- 0 duplicate executive + month; every `targetdate` on the 1st; no remark over 500 characters.
- Principal split: **205 / 205** lines written into the remarks — nothing cut.

### Campaign (module 15)

- `mmcampaign` **8**, all status **Sent** (2); **0** recipients (`mmcampaignemails`) and **0** template mappings → nothing can be mailed.
- Titles / scheduled dates / subjects as in eBizWiz (CA1 "aa" 10/03/2016 … CA8 "Mails : Sales Team" 23/08/2021).

### Expenses (module 16)

- `expense` **3** (UE1 × offices 2, 3, 6); `expensedetails` **2** = **₹1,250** (₹1,000 travelling + ₹250 food allowance, office 6).
- **1** Sanctioned (office 2) with its `expensestatus` row; the other 2 "Not Entered".
- `ExpCategory` seeded **3**.

### Task (module 17)

- `task` **5** + `ticket` **5** (category `TSK`).
- Status: Close **1** / Open **4**. Priority: High **4** / Medium **1**.

## 7. Notes

- **Yearly → monthly.** The target grid and dashboard work month by month, so each yearly period becomes 12 equal monthly rows; putting the whole year on one month would show it as April's target.
- **Manish Saini FY 2017.** Both yearly targets (₹1 Cr + ₹2 Cr) are real client entries and the app keeps one row per executive / module / month, so they are summed into one year (₹3 Cr → ₹25 L a month) and both target numbers are named in the remark.
- **Principal split as text, not supplier rows.** `target.scode` exists, but the achievement figure (`sum(tamt) from target where module … and executive … and targetdate between …`, `app/include.asp`) and the bulk grid (`order by code desc`, `app/target.asp`) both ignore `scode` — an overall row plus supplier rows would double the target. On 3 targets (office 3, FY 2013-14) the split also adds up to ₹5.20 Cr against a yearly target of ₹3.70 Cr.
- **Target edit form.** The standard target edit form fills the executive from index 13 of the target array instead of index 1 (`assets/scripts/target.js`, `handleFormFill`). This is app behaviour and is not changed.
- **Campaign → Mass Mail is safe.** Every send path works off recipient rows: `addCampaign` queues `mmcampaignemails` (`sent = 0`) + `mmemailtempaltemapping`, and the contact screens only look at `mmcampaignemails where sent = 0` (`app/massmail.asp`, `app/contact.asp`); there is no sender inside the web project. With a header and no recipient, nothing can ever be mailed. Status is Sent because these are finished, approved campaigns (2016–2021), not pending ones.
- **Test-like campaigns.** All 8 campaigns are written, CA1 "aa", CA6 "Test CAmpaign" and CA7 "test" included — they are client data in real offices; they can be deleted in the app if not wanted. Nothing in eBizWiz links an inquiry to a campaign (no campaign column on any transaction table).
- **Re-runs.** Module 14 first deletes `target` rows with `module = 'SAL'` (only this module writes them). Module 15 deletes only the headers it wrote itself (migration user + the `Migrated from eBizWiz Campaign` body marker + still no recipient).
- **Expense header remark.** `expense` has no remark column, so the eBizWiz header remark goes into the first line's Narration as `Expense remark: …`.
