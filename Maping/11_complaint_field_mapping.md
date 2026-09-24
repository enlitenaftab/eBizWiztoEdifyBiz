# 11. Call Entry (Service Entry) → AMC Complaint — field mapping

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | Service → Call Entry (`Contracts/call.aspx`, title "Service Entry") | AMC → Complaint |
| Tables | `trhcalls` (36,164) · `trdcalls1visit` (56,248) · `trdcalls2fault` (32,522) · `trdcalls3parts` (6,858) · `trdcalls8checks` (224,583) · `trdcalls10pendingparts` (5) · `trdcalls6posttaxchgs` (8) · masters `mstcheckset` / `msdcheckset` / `mstchecks` | `contractcall` · `callvisits` · `amcusedproduct` · `amcrequestedproduct` · `taskchecklist` (`ACM`) · `labelrelation` (category `AMC`) · spares MRO `stockinout` / `stockinoutdet` / `stocktrans` · Checklist Master `taskchecklistmaster` / `taskchecklistmasterdet` · call bills as Sales Invoices `sal_order` / `sal_order_det` / `sal_tax` / `sal_adjust` |
| Code | — | form + view `amc/complaint/default.asp`, JS `amc/scripts/complaint.js`, backend `amc/app/complaint.aspx.cs` (`GetComplaintArray`); check list tab `assets/scripts/taskchecklistfill.js` |
| Exe | — | module **11. Complaint (Call Entry)**, class `Complaint`, then `CallBillInvoice` (needs 1, 2, 8, 9/10 first) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) | branches 3, 4, 5, 6 |
| Status | | migrated and verified |

One client call = one `contractcall` row. PM VISIT calls that already exist as a PMS row (from the AMC contract or the warranty, module 10) **update** that row instead of adding a second one. A call bill with a bill number and a non-zero amount becomes a Sales Invoice labelled "Service Call Bill".

---

## 2. Field mapping

Everything marked **remark** goes into `contractcall.remark` as `Label: value` parts joined with ` | `, in this order: Comment, Call Type, Call No / Trn No / office, Call Logged By, Person Calling, Transfer To, Location, Change Item, Warranty Void, Purchase / Start Date, Months, Product Condition, Allocation Date, Accepted, Bill No, Pending Reason, Cancel Reason, Fast Close, Validation Done, faults without a visit.

### 2.1 Top bar

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Unique Call No. | `trhcalls.vtrnno` | `contractcall.pmscomplaintno` | Complaint No (read-only) | Complaint No | as is, e.g. `26E30007`; 0 duplicates |
| 2 | Trn. No. | `vtrnprefix` + `ntrnno` | `reportno` | Report No | Report No | e.g. `CL16505` |
| 3 | Call Type | `ncalltype` → `mstfixedselection` | `type` + label | — (set by the add action) | Complaint Type (shows `type`) + Labels | §7 type rule; exact call type as a label (category `AMC`) |
| 4 | Date | `dtrndate` | `complaintdate` | Complaint Date | "Complaint Date" in the Created/Updated block | when the call was logged |
| 5 | Call Logged By / Edited By | `addedby` / `editedby` → `mstusers` | `contractcall.createdby` / `updatedby` (+ remark "Call Logged By") | — | Created By / Updated By (top right) + Remark | user map; migration user when the client user is unknown. Updated By added 23/09/2026, verified in the DB (34 distinct users) |

### 2.2 Party Details

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 6 | Party | `nparty` | `ccode` | Customer | Customer | contact map (module 1); for a linked call the list/view read the customer from the contract |
| 7 | Contact Person | `npartycontact` | `contactperson` | Contact Person | Contact Person | party's `mltcontact` row, matched by name |
| 8 | (party branch) | — | `contactbranch` | Branch | Contact Branch | the contact's `mltaddress` |
| 9 | Person Calling | `vpersoncalling` (325) | remark | Remark | Remark | no column of its own |
| 10 | Transfer To | `nTransferTo` (office) | remark | Remark | Remark | office name |
| 11 | Telephone 1 / 2, Filter Party by City | party master | — | — | — | display only on the client too |

### 2.3 Item Details

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 12 | Item / Item Code | `nitem` | `productcode` | Product (Walk In; otherwise from Serial Number) | Product | product map (module 2); contract line's product when linked |
| 13 | Serial No. | `vserialno`, `nitemserialno` | `serialno` + `contractdtcode` | Serial Number · Contract Number | Serial No · Contract No · Walk In Serial No | contract line resolved by the §7 contract-line rule |
| 14 | Location | `vlocation` | remark | Remark | Remark | |
| 15 | Warranty Void | `bwvoid` (13) | label `Warranty Void` + remark | Labels | Labels · Remark | |
| 16 | Change Item | `bitemchange` | remark | Remark | Remark | "Change Item: Yes" |
| 17 | Purchase / Start Date | `ddateofpurchase` | remark | Remark | Remark | warranty dates also live on the warranty contract (11 §2.6) |
| 18 | Months | `nwarrmonth` | remark | Remark | Remark | |
| 19 | Item Status [W-C-O], Total Calls, Outstanding Amt., Start / End Date, Manufacturer Name | computed / item master | — | — | — | our view computes its own from the contract |

### 2.4 Complaint Details

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 20 | Complaint Reported | `vcomplaint` | `servicecomplaint` | Service Complaint | Service Complaint | |
| 21 | Our Complaint | `ncomplaint` → `mstcomplaint` | `complaint` → `complainttype` | Complaint | Complaint | master seeded by name (§3) |
| 22 | Special Instructions | `vspecialinstructions` | `Observation` | Observation | Observation | |
| 23 | Product Condition / Accessory Recd. | `vsetcondition` | remark | Remark | Remark | |

### 2.5 Allocation & Acceptance

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 24 | Service Person | `nengineer` → `mstusers` | `assignedto` | Assigned To | Assigned To | user map |
| 25 | Appointment Date | `dapptdatetime` | `date`, `preferredtimein`, `preferredtimeout` | Visit Date · Preferred Time In | PMS Date (`date`) · Visit Date (`preferredtimein`) | out = +2 hrs (app rule); no appointment → call date. PMS rows being updated keep their scheduled `date` |
| 26 | Allocation Date | `dallocationdatetime` | remark | Remark | Remark | |
| 27 | Check List | `ncheckset` → `mstcheckset` + items `trdcalls8checks` | items → `taskchecklist` module `ACM`; the set → Checklist Master (`taskchecklistmaster` / `taskchecklistmasterdet`) | — | Task Check List tab | items as in §2.8 row 45; the set name is not written into the remark |
| 28 | Accepted Status / Date | `baccepted`, `dacceptdate` | remark | Remark | Remark | "Accepted on dd/MM/yyyy" |

### 2.6 Estimate & Billing

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 29 | Chargeable Amount | `ntotalamount` | `callamt` | Call Amount | Call Amount | |
| 30 | Bill No. | `vinvoiceprefix` + `ninvoiceno` + `dinvoicedt` | remark + **Sales Invoice** (`sal_order`, no. = bill no., e.g. `BS510`) | Remark | Remark; Sales Invoice list (label "Service Call Bill") | "Bill No: X dt. d" in the remark. `CallBillInvoice` turns every call bill with a bill number and a non-zero amount (960 of 983) into a Sales Invoice: customer / contact / Bill To / engineer (executive) from the migrated complaint; lines = the call's parts (`trdcalls3parts`); tax + post-tax charges (`trdcalls6posttaxchgs`) by the Sales Invoice rules; total = `ntotalamount`; remark e.g. `Service Call: 26E30007 \| Trn No: CL16505`. Receipts against calls are allocated to it (12). No SAL stock row: the spare lines already left stock through the call MRO (row 46). **Changed 23/09/2026 (verified in the DB 24/09):** the invoice also carries the standard link back to the complaint - `sal_order.module = 'AMC Complaint'`, `modulecode` = the complaint code (this is what the complaint view's "View Invoice" tab reads; that tab is company-gated to Zinq / Skytech / Cona today) |
| 31 | Repair Estimate No./Date, Estimate Amt, Approved By, Customer PO No./Date | `nestimateamt`, `vapprovedby`, `npono`, `dpodate`, `trhRprEst` | — | — | — | §5 (no data) |

### 2.7 Call Status & Customer Follow-Up

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 32 | Solved + Solved Date | `bsolved`, `dsolvedatetime` | `status` Closed + `closedt` | Status | Status | §7 status rule |
| 33 | Cancel Reason | `ncancelreason` → `mstcallcancelreasons` | `status` Cancelled + remark | Status · Remark | Status · Remark | only when not solved |
| 34 | Solved Remarks | `vsolveremarks` | `servicedetails` | Service Details | Service Details | |
| 35 | Comment | `vcomment` | remark (first part) | Remark | Remark | |
| 36 | Pending Reason | `npendingreason` → `mstcallpendingreasons` | remark | Remark | Remark | a solved call can carry one too, so it is not a status |
| 37 | Fast Close + date | `bfastclose`, `dfastclosedatetime` | remark | Remark | Remark | |
| 38 | Validation Done | `bvalidation` | remark | Remark | Remark | "Validation Done: Yes" |
| 39 | Follow Up Date / Follow Up By | `dfollowupdt`, `vfollowupby` | `followup` (module `ACM`, `modulecode` = complaint, `nextfollowup`, remarks "Customer follow-up (eBizWiz) - Follow Up By: …") | — | **Followup** tab | one follow-up per call that has either value |
| 39a | Escalated To, Follow Up No. | `nescalateto`, `nfollowupno` | — | — | — | 0 filled (§5) |
| 40 | Select Letterhead To Print | `vletterhead` | — | — | — | print choice only |

### 2.8 Children

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 41 | Visit grid | `trdcalls1visit` | `callvisits` | — | Call Visit Logs tab (Complaint / Date / Status / Assigned To / Call Amount / Remarks) | `dvisitdatetime`→`date`, `nvisitby`→`assignedto`, call's complaint type→`complaint`, visit's fault solved→Closed else Open; remark = `vVisitTrnNo` + `vvisitremark` + `vcustomerRemarks` + `vPartsReplacedDetails` + time spent + kms; call's `ccode` / `productcode` / `type` / `pmscomplaintno` copied (the app does the same) | Call Visit Logs **Complaint** column = the visit's own "Our Complaint" from its first fault row (else the call's).
| 42 | Fault grid | `trdcalls2fault` → `mstcomplaint` / `mstdefect` / `mstrepair` | `callvisits.remark` of its visit | — | Call Visit Logs → Remarks | `Complaint: x \| Defect: y \| Repair: z \| remarks`; a fault with no visit goes to the call remark |
| 43 | Parts grid | `trdcalls3parts` | `amcusedproduct` | — | Used Product tab | `nitem`→`pcode`, `nquantity`→`qty`, unit, visit engineer→`executive`, `nrate`→`quotationcost`; spare location / def. qty / claimed / remarks → `remark` |
| 44 | Parts pending | `trdcalls10pendingparts` | `amcrequestedproduct` | — | Requested Product tab | |
| 45 | Check list | `trdcalls8checks` → `mstchecks` | `taskchecklist` (`ACM`) | — | Task Check List tab | `ncheck`→`title`, `bdone`→`status`, `dexpdate`→`duedate`, `nallotedto`→`assignto`, `vremarks`→`remark` |
| 46 | Parts used (stock) | `trdcalls3parts` of SPARES items | `stockinout` MRO + `stockinoutdet` + `stocktrans` −qty | — | Inventory → MRO (number = call no.) | only stock products; LABOUR (service charge) rows stay on Used Product only |

---

## 3. Masters seeded

| Master | Rows | Source |
|---|---|---|
| `status` module AMC | `Open` (Pending), `Closed` (Completed), **`Cancelled`** (Completed) | Open / Closed from module 10; Cancelled by this module |
| `complainttype` | 100 client values (102 total with the 2 demo rows) | `mstcomplaint`, matched by name |
| `label` category `AMC` | 10: P M VISIT, Breakdown (Field) Call, INSTALLATION CALL, COURTESY CALL, OTHERS, VALIDATION CALL, DEMO CALL, INSPECTION CALL, PAID SERVICE CALL, Warranty Void | `mstfixedselection` call types + `bwvoid` |
| Checklist Master `taskchecklistmaster` / `taskchecklistmasterdet` | 24 sets / 253 items (title, days, sort) | `mstcheckset` + `msdcheckset` (item names from `mstchecks`, days from `ndaysdiff`); sets of offices 2/3/4/6 that are active or used by a migrated document; one shared master for all modules, matched by title |
| `label` category `SAL` | "Service Call Bill" | marks the call bill invoices in the Sales Invoice list (12) |

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | Complaint view | "Task Check List" tab + empty pane `#view_taskchecklist_div` + `taskchecklistfill.js` include | Layout | `amc/complaint/default.asp` | `taskchecklist` (`ACM`) |
| 2 | Complaint view | `TaskCheckListFill.init()` and fill on view (module `ACM`) | Behaviour | `amc/scripts/complaint.js` | `taskchecklist` |
| 3 | Complaint view | Customer Rating / Customer Rated On / Customer Feedback Remark block opened for saksham | Layout | `amc/complaint/default.asp` | `customerfeedback` → see 14 |
| 4 | Complaint view | saksham fill of those three fields (the original fill is inside the Zinq/Skytech gate) | Behaviour | `amc/scripts/complaint.js` | `customerfeedback` → see 14 |

| 5 | Complaint view | **View Invoice** tab + pane opened for saksham (the tab exists in EdifyBiz but was gated to Zinq / Skytech / Cona). Shows the call bill of that complaint; reads `action=salInvoiceList` → `sal_order.module = 'AMC Complaint'`, `modulecode` = complaint. The "Link Invoice" button is left out: the link comes from the migration | Layout | `amc/complaint/default.asp` | `sal_order` |

**New fields: 0** — every header value uses a field saksham already has. Rows 3–4 serve the Customer Feedback module (13), row 5 shows the call bill.

### 4b. Database changes (ALTER)

None.

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Repair Estimate / Estimate Amt / Approved By / Customer PO | `trhRprEst`, `nestimateamt`, `vapprovedby`, `npono`, `dpodate` | 0 on offices 2/3/4/6 (10 in `trhRprEst` in total) | no data |
| Escalated To / Follow Up No. / By / Date | `nescalateto`, `nfollowupno`, `vfollowupby`, `dfollowupdt` | 0 / 0 / 1 / 18 | practically empty |
| Letterhead | `vletterhead` | 3,297 | print option, not data |
| Call bills with amount 0 | `trhcalls` with `ninvoiceno`, `ntotalamount = 0` | 23 | nothing billed, no receipt on them — no Sales Invoice; the bill no stays in the remark |
| Out-repair, stand-by, check-visits, stand-by missing | `trdcalls4outrp`, `trdcalls7standbyreplace`, `trdcalls9checksvisits`, `trdcalls9stbymissing` | 0 | empty |
| Used-product lines of items never migrated | `trdcalls3parts` | 11 | item not in our product master |
| Customer Feedback / Survey | `trdcalls5happy` | 41,256 | its own module → `13_customerfeedback_field_mapping.md` |
| Calls of offices 1 and 5 | `trhcalls` | 280 | WinMax test offices |

---

## 6. Verification (latest run)

**Header — every count equals the client (offices 2/3/4/6, blank / whitespace values not counted):**

| Check | Client | Ours |
|---|---|---|
| Calls | 36,164 | 36,164 (`reportno` filled), 0 duplicate call numbers, 0 rows without `date` |
| Complaint Reported → Service Complaint | 36,164 | 36,164 |
| Our Complaint → Complaint | 28,681 | 28,681 |
| Service Person → Assigned To | 32,512 | 32,512 |
| Special Instructions → Observation | 1,512 | 1,512 |
| Solved Remarks → Service Details | 6,640 | 6,640 |
| Chargeable Amount > 0 → Call Amount | 1,215 | 1,215 |
| Solved → Closed + `closedt` | 31,169 | 31,169 |
| Cancelled (not solved) | 4,767 | 4,767 |
| Open | 228 | 228 |
| Call bills → Sales Invoices ("Service Call Bill") | 983 bills (23 at amount 0), ₹2,41,96,132.26 | 960 invoices, total ₹2,41,96,132.26 |

**Type:** PMS 17,177 · Complaint 9,621 · Installation 3,616 · Walk In 5,750 = 36,164.
**Contract link:** 30,414 calls on a contract line — AMC 18,132 (quotation-based 17,806 + direct 326) and warranty 12,282; Walk In 5,750 (6 on the test item "testing 123"). Complaint calls outside their line's period: **0**.
**PMS rows without a call:** 2,176 (AMC 1,289 + warranty 887) — these PM visits have no call in eBizWiz either; 2,175 Open, 1 Closed.

**Children:** call visits 56,248 / 56,248 · check list 224,583 / 224,583 (`ACM`) · used products 6,847 (+11 not migrated) · requested products 5 / 5 · labels 36,177 (P M VISIT 17,181, Breakdown 13,257, Installation 3,649, Courtesy 1,375, Others 292, Validation 291, Demo 78, Inspection 40, Warranty Void 13, Paid Service 1).
**Stock:** spares MRO 106 documents / 141 lines. Closing stock per branch + item = the eBizWiz balance (`mststkdt`) on **4,352 / 4,352** stock-product keys, after the opening stock (module 8) and the "eBizWiz balance adjustment" JVs posted at the end of this module (`Shared.PostBalanceAdjustment`, see 10 row 38a).

**Form visibility (read in `default.asp`, every company gate resolved):** Complaint No, Report No, Complaint Date, Customer, Branch, Contact Person, Serial Number, Contract Number, Complaint, Assigned To, Status, Call Amount, Visit Date, Preferred Time In, Service Complaint, Observation, Service Details, Remark are all shown for saksham; on view also PMS Date, Complaint Type, Labels and the Call Visit Logs / Used Product / Requested Product / Task Check List tabs.

**Record check — call `26E30007`** (HO Mumbai): Trn `CL16505`, complaint date 30/05/2026 13:50, visit 13:55, customer / product / serial, Our Complaint "Machine Not Working", Closed 30/05/2026 15:10, engineer Niraj Sharma, call amount 17,700, 1 visit ("26E30007 - 01" + its fault), 1 used product (SERVICE CHARGES 15,000), 14 check-list items, label "Breakdown (Field) Call"; remark starts `Call Type: Breakdown (Field) Call | Call No: 26E30007 | Trn No: CL16505 | SAKSHAM - HO MUMBAI | Call Logged By: Amit Gavkar | Transfer To: …` — same as eBizWiz.

---

## 7. Notes

**Type rule** — only the app's four values are used: P M VISIT → `PMS`; INSTALLATION CALL (codes 706 + 794) → `Installation`; everything else → `Complaint`; any call with no contract line → `Walk In` (`contractdtcode = 0`), the only shape in which our form shows a product that is not under contract. The exact eBizWiz call type is kept as a label.

**Contract-line rule** — a call links only to the AMC or warranty line whose period covers the call date; an INSTALLATION call before the start takes the serial's line starting soonest after it (warranty starts at installation). A call outside every line's period is a Walk In, so no call is tied to an expired contract.

**PM VISIT calls** — module 10 creates PMS rows for AMC PM visits (`trdcontr7pmvisit`) and warranty PM visits (`trdsales7pmvisit`). A call named on such a visit (`ncalls`) updates that row, found by its own number `PMS` + 7-digit `contractdetails.code` + `_` + visit index (rebuilt in module 10's read order); `(contractdtcode | scheduled date)` is only a fallback, because 28 serials have two PM visits on the same date. The update keeps the scheduled date, type and contract link.

**Status rule** — `bsolved = 1` → Closed + `closedt`; else a cancel reason → Cancelled; else Open.

**Check lists** — the items of each call are its Task Check List (`taskchecklist`, module `ACM`, the standard reusable tab); the eBizWiz check set itself is a Checklist Master template, so the set name is not repeated in the remark.

**Call bills** — eBizWiz bills a call on the Service Entry itself; EdifyBiz bills service through a Sales Invoice and the complaint has no invoice link, so the invoice names the call in its remark and carries the label "Service Call Bill" (module 12 finds it by that label + branch + bill no).

**How the app behaves (read in `complaint.aspx.cs`)** — on save the app overwrites `date` / `preferredtimein` with Visit Date + Time for every type except PMS; the list filters on `date`, so `date` is never NULL; every save also adds a `callvisits` row; the complaint module never touches stock, which is why spares get their own MRO (D1: only SPARES items; the 6,706 LABOUR rows are service charges with no stock).

**Decisions** — D1 spares MRO for stock items; D2 status `Cancelled`; D3 no contract → `Walk In`; D4 check lists → `taskchecklist` `ACM` with the standard tab, check sets → Checklist Master; D5 seed `complainttype` from `mstcomplaint`; D6 call type + Warranty Void as labels; billed calls (bill no + non-zero amount) → Sales Invoice "Service Call Bill".

**Tell the client (settings, not migration issues)** — (1) the `autonumber` row "AMC Complaint" has `allowautogenration = 'N'` and Complaint No is read-only, so new complaints get a blank number until it is switched on; (2) quick-close or moving to a Completed status needs at least one DMS file (saksham is not in the exemption list) — applies to migrated calls too; (3) Payment Terms is `required` on the saksham form but the client has no such field, so editing an old complaint asks for it.

_Read from `saksham70v1_1`, `SakshamRMtP15329`, `Program.cs` (classes `Complaint`, `CallBillInvoice`, `EnsureChecklistMasters`) and `amc/complaint/default.asp`, `amc/scripts/complaint.js`, `amc/app/complaint.aspx.cs`._
