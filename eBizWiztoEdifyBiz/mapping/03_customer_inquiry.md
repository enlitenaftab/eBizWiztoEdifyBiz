# 03 — Customer Inquiry

| | |
|---|---|
| **Client screen** | eBizWiz → `Inquiry > Inquiry Entry` (header + Inquiry Progress Details), and from **More**: Actions, Items, Competitors, Check List |
| **Our screen** | EdifyBiz → `Inquiry > Customer Inquiry (CI)` + Follow Up |
| **Source tables** | `trhinqry` (header), `trdinqry1prods` (items), `trdinqry2actions` (actions), `trdinqry3competitor` (competitors), `trdinqry4checks` (check list) |
| **Target tables** | `inqcs` (`cors='CI'`), `inqcsdet`, `followup` (`module='INQ'`), `taskchecklist` (`module='CI'`) |
| **Code** | `Program.cs` → `static class CustomerInquiry` (+ nested `Followup`) |

One Inquiry Entry becomes one `inqcs` row, one `inqcsdet` row per item, one `followup` row per action, and one `taskchecklist` row per check.

---

## 1. Migrated

### 1.1 Inquiry Entry header → `inqcs`  (main form)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Trn. No. | `trhinqry.vtrnprefix` + `ntrnno` | Inquiry No. | `inqcs.inqref` | Joined, e.g. `IQ71838` |
| 2 | Trn. Date | `trhinqry.dtrndate` | Inquiry Date | `inqcs.inqdate` | |
| 3 | Party | `trhinqry.nparty` → `contact` | Company Name | `inqcs.cscode` | Resolved through the migrated Contact; its default address goes to `inqcs.csbranch` |
| 4 | Contact Person | `trhinqry.npartycontact` → `mltcontact` | Contact Person | `inqcs.cperson` | Source person number resolved to the migrated `mltcontact.code` |
| 5 | Sales Person | `trhinqry.nsalesman` → `users` | Executive | `inqcs.executive` | Falls back to the migration user |
| 6 | Inquiry By | `trhinqry.ninquiryby` → `mstfixedselection` | Inquiry By | `inqcs.calltype` | → `miscellaneous(Inquiry / Call Type)` |
| 7 | Category | `trhinqry.ninquirycategory` → `mstinquirycategory` | Category | `inqcs.Category` | → `miscellaneous(Inquiry / Category)` |
| 8 | Inquiry Details | `trhinqry.vinquirydetails` | Inquiry Details | `inqcs.additionalinstruction` | Own field — no longer part of the remark |
| 9 | Source | `trhinqry.ninquirysource` → `mstinquirysource` | Source | `inqcs.sources` | Name stored, not code |

### 1.2 Inquiry Progress Details → `inqcs`  (Inquiry Progress section)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Sales Stage | `trhinqry.nsalesstage` → `mstsalesstage` | Sales Stage | `inqcs.stage` | → `miscellaneous(Inquiry / Sales Stage)` |
| 2 | Approx. Close Date | `trhinqry.dclosingdate` | Valid Till | `inqcs.validdate` | |
| 3 | Status | `trhinqry.ninquirystatus` → `mstfixedselection` | Status | `inqcs.status` | → `status` master |
| 4 | Lost Reason | `trhinqry.nlostreasons` → `mstorderlostreason` | Lost Reason | `inqcs.lossreason` | → `miscellaneous(Inquiry / Loss Reason)`. Enabled only when the status is LOST |
| 5 | Probability % of Getting The Order | `trhinqry.nprobability` → `mstprobability` | Probability % of Getting The Order | `inqcs.inqprobability` | → `miscellaneous(Inquiry / Probability)` |
| 6 | Inquiry Value | `trhinqry.ninqvalue` → `mstfixedselection` | Inquiry Value | `inqcs.inqvalue` | → `miscellaneous(Inquiry / Inquiry Value)` |
| 7 | Remarks | `trhinqry.vremarks` | Remark | `inqcs.remark` | Skipped when identical to Inquiry Details |
| 8 | — | `trhinqry.nactionType` → `mstfixedselection` | Type | `inqcs.types` | `SALES LEAD` / `DSR - ACTION NEEDED` / `DSR - NO ACTION NEEDED`. Drives which follow-up block the form shows |
| 9 | (record office) | `trhinqry.nofficeid` | Branch | `inqcs.branchcode` | Office → `companyaddress` branch |
| 10 | (audit) | `addedon` / `addedby` | Created On / By | `inqcs.createdon` / `createdby` | |
| 11 | (audit) | `editedon` / `editedby` | Updated On / By | `inqcs.updatedon` / `updatedby` | Falls back to added on / by |

### 1.3 Items → `inqcsdet`  (Items screen, from More)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Item Name / Item Code | `trdinqry1prods.nitem` → `product` | Product | `inqcsdet.pcode` | Resolved through the migrated Product; a line whose item does not resolve is skipped and counted |
| 2 | Quantity | `trdinqry1prods.nquantity` | Quantity | `inqcsdet.quantity` | Source stores it ×1000 — divided by 1000 on the way in |

Item Name and Item Code are two lookups for the same item in eBizWiz; EdifyBiz holds one product reference, and the Name / CAS# come from the Product master.

### 1.4 Actions → `followup` (`module='INQ'`)

Two places on the client feed the same table: the **first action is entered inline on the Inquiry Entry ADD screen**, and later actions are added from the **Actions screen (More)**. Both are rows of `trdinqry2actions`, so both migrate the same way.

**Which action blocks the client shows depends on Type** — our form follows the same rule:

| Type | 1st Action Details | Action Taken Detail | Next Action To Be Taken | Status default |
|---|---|---|---|---|
| `SALES LEAD` | shown | hidden | hidden | OPEN |
| `DSR - NO ACTION NEEDED` | shown | shown | hidden | DSR CLOSED |
| `DSR - ACTION NEEDED` | shown | shown | shown | OPEN |
| (none chosen) | only the Type field | hidden | hidden | — |

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Action Taken, else Purpose / Next Action To Be Taken | `nactiontaken` → `mstactiontaken`, else `nactiontobetaken` → `mstactiontobetaken` | Action Taken / Next Action To Be Taken | `followup.remarkscode` | Name resolved to `followupremarks`; missing names are added |
| 2 | — | derived from the activity name | Type | `followup.followType` | Mapped to `Call` / `Meeting` / `E-Mail` / `SMS` / `Fax` / `Others` by keyword |
| 3 | Purpose (1st Action Details) | `nactiontobetaken` → `mstactiontobetaken` | Purpose | `followup.purpose` | Name stored as text |
| 4 | Priority | `npriority` → `mstfixedselection` | Priority | `followup.priority` | |
| 5 | Action Taken Remarks | `vremarks` | Remarks | `followup.remarks` | |
| 6 | Purpose Remarks / Action To be Taken Remarks | `vactiontoberemarks` | Remarks | `followup.remarks` | Appended as `Purpose Remark: …`, only when it differs from the Action Taken Remarks |
| 7 | Action Taken Date | `dactiondate` | Action Taken Date | `followup.lastfollowup` | |
| 8 | Date (1st Action) / Action To Be Taken Date | `dactiontobedate` | Next Follow Up | `followup.nextfollowup` | |
| 9 | Person Contacted | `npartycontact1` → `mltcontact` | Person Contacted | `followup.person` / `pcode` | Falls back to Party Contact (`npartycontact`) when empty |
| 10 | — | the inquiry's Party | Client | `followup.ccode` | Taken from the parent inquiry |
| 11 | Sales Person | `nsalesman` → `users` | Created By | `followup.createdby` | |
| 12 | — | the inquiry's Status | Status | `followup.status` | Open inquiry → `In-Process`, otherwise `Close`. A NULL status would make the row invisible on every follow-up screen |
| 13 | (audit) | `addedon` | Created On | `followup.createdon` | |

An action whose inquiry did not migrate is skipped and counted.

### 1.5 Check List → `taskchecklist` (`module='CI'`)  (Check List screen, from More / Task Check List tab)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Check | `trdinqry4checks.ncheck` → `mstchecks.vname` | Title | `taskchecklist.title` | Falls back to `Check <code>` when the name is missing |
| 2 | Done | `bdone` | Status | `taskchecklist.status` | Done → 1, else 0 |
| 3 | Expected Date | `dexpdate` | Due Date | `taskchecklist.duedate` | Empty → done date, else today + 7 days |
| 4 | Alloted To | `nallotedto` → `users` | Assign To | `taskchecklist.assignto` | |
| 5 | Remarks | `vremarks` | Remark | `taskchecklist.remark` | |
| 6 | (grid order) | `ncode` | — | `taskchecklist.sort` | |
| 7 | (audit) | `addedon` / `editedon` | Created / Updated On | `taskchecklist.createdon` / `updatedon` | For a done check, `ddonedate` is used as updated on |

No `task` record is created — the items live in the Task Check List tab only.

### 1.6 Masters seeded

| Source | Target |
|---|---|
| `mstfixedselection` (`ninquirystatus`) | `status` |
| `mstinquirycategory` | `miscellaneous(Inquiry / Category)` |
| `mstinquirysource` | `miscellaneous(Inquiry / Inquiry Source)` |
| `mstsalesstage` | `miscellaneous(Inquiry / Sales Stage)` |
| `mstorderlostreason` | `miscellaneous(Inquiry / Loss Reason)` |
| `mstprobability` | `miscellaneous(Inquiry / Probability)` |
| `mstfixedselection` (`ninquiryby`) | `miscellaneous(Inquiry / Call Type)` |
| `mstfixedselection` (`ninqvalue`) | `miscellaneous(Inquiry / Inquiry Value)` |
| `mstfixedselection` (`nactionType`) | `miscellaneous(Inquiry / Action Type)` |
| `mstactiontaken` + `mstactiontobetaken` | `followupremarks` |

---

## 2. Folded into a text field

All of these land in `inqcs.remark`, separated by ` | `.

| # | Client UI | Client table.column | Written as |
|---|---|---|---|
| 1 | Comment | `trhinqry.vcomment` | `Comment: <value>` |
| 2 | Campaign | `trhinqry.ntrhcampa` → `trhcampa` | `Campaign: <name>`, or `Campaign: #<code>` when the name is not found |
| 3 | Competitor (whole screen) | `trdinqry3competitor` | `Competitor: <party name> (Product: …, Feature: …, Price: …, Order Won: Yes/No)`, one entry per competitor row |

The Competitor screen has no structure of its own in EdifyBiz, so all five of its fields — Competitor, Product & principal, Remarks (`vfeature`), Price Won, Order Won — are written as one line in the inquiry remark. The source holds only **14 competitor rows** in all four offices.

---

## 3. Skipped

Every column below was searched across the whole of `Program.cs`, not just this module — none is read anywhere.

### 3.1 `trhinqry` (header)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nquotation`, `nquotedvalue` | yes — Quote No., Quote Value | Read-only on the client screen. In EdifyBiz the quotation carries the link back to its inquiry, so the number comes from the real document instead of a copied value |
| 2 | `norderno`, `nordwonvalue` | yes — Order No., Order Value | Same — the sales order carries the link |
| 3 | `ncheckset` | yes — Check List | The check-list **set name** is not stored; its items migrate from `trdinqry4checks` (section 1.5) |
| 4 | `nordermargin`, `nmargin` | no | Margin figures, no EdifyBiz equivalent |
| 5 | `bapproval`, `approvedby`, `approvedon` | no | The client's inquiry screen has no approval step |
| 6 | `rowguid` | no | Internal key |
| 7 | `maddedby`, `maddedon`, `meditedby`, `meditedon`, `mlatlong` | no | Mobile-app audit and coordinates; the web audit columns are migrated instead |
| 8 | `ipaddress` | no | Audit only |

### 3.2 `trdinqry2actions` (Actions)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nexpense` | yes — Expense Amt. | `followup` has no expense column |
| 2 | `nkms` | yes — Kms | `followup` has no kilometre column |
| 3 | `nsalesmanforactiontaken` | yes — Sales Person (under Action Taken) | `followup` holds one person; the Action To Be Taken salesperson is used |
| 4 | `bemail` | no | Email-sent flag |
| 5 | `maddedby`, `maddedon`, `meditedby`, `meditedon`, `mlatlong` | no | Mobile-app audit |
| 6 | `addedby`, `editedby`, `editedon`, `ipaddress` | no | `followup` records only created by / created on |

### 3.3 `trdinqry4checks` (Check List)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nexpense` | no | `taskchecklist` has no expense column |
| 2 | `npendingreason` | no | `taskchecklist` has no pending-reason column |
| 3 | `addedby`, `editedby`, `ipaddress` | no | Audit only |

### 3.4 `trdinqry1prods` (Items)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Line audit; the inquiry's dates are used on every line |
