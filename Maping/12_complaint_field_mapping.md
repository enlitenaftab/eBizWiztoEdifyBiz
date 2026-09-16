# 12. Call Entry (Service Entry) → AMC Complaint — field mapping

Client menu: **Service → Call Entry** (`Contracts/call.aspx`, form title "Service Entry")
Our module: **AMC → Complaint** (`/edify/amc/complaint/`, backend `amc/app/complaint.aspx.cs`, table **`contractcall`**)

> Status: **mapping for confirmation** — exe not written yet. Nothing here is assumed; every row below was read from the client DB (`saksham70v1_1`), our DB (`SakshamRMtP15329`) and the project code.

---

## 1) Volumes (offices 2/3/4/6 only — 1 and 5 are WinMax test offices)

| Client table | Rows (real offices) | What it is |
|---|---|---|
| `trhcalls` | **36,164** | Call header (all 36,444 − 280 test) |
| `trdcalls1visit` | **56,248** | Engineer visits |
| `trdcalls2fault` | **32,522** | Fault / defect / repair per visit |
| `trdcalls3parts` | **6,858** | Spares used per visit |
| `trdcalls8checks` | **224,583** | Check-list results |
| `trdcalls5happy` | 41,656 | Customer feedback questionnaire (see §8) |
| `trdcalls4outrp`, `trdcalls7standbyreplace`, `trdcalls9checksvisits`, `trdcalls9stbymissing` | **0** | empty |
| `trdcalls10pendingparts` | 5 | pending parts |
| `trdcalls6posttaxchgs` | 8 | tax charges on a call bill |

Call types (`ncalltype` → `mstfixedselection`, page `call.aspx`):

| Client call type | Calls | Linked to a contract PM visit (`trdcontr7pmvisit.ncalls`) |
|---|---|---|
| P M VISIT | 17,181 | **12,930** |
| Breakdown (Field) Call | 13,257 | 0 |
| INSTALLATION CALL (706 + 794) | 3,649 | 0 |
| COURTESY CALL | 1,375 | 0 |
| OTHERS | 292 | 0 |
| VALIDATION CALL | 291 | 0 |
| DEMO CALL | 78 | 0 |
| INSPECTION CALL | 40 | 0 |
| PAID SERVICE CALL | 1 | 0 |

Serial linkage: 23,027 calls have their `nitemserialno` in a contract line (`trdcontr2itemsdet`), **17,226** of those fall inside that contract's period; 27,547 calls carry a serial that was sold (`trdsales2itemsdet`).

---

## 2) Where it lands in our project (checked in code, not guessed)

- One call = one row in **`contractcall`** — the same table as the PMS visits migrated in module 10; `type` separates them.
- The list (`amc/app/complaint.aspx.cs`, `listcomplaint`) shows **all** `contractcall` rows and filters on `cast(a.date as date)`, so `date` must be filled.
- Valid `type` values (from the list filter in `amc/complaint/default.asp:280`): **PMS, Complaint, Walk In, Installation**.
- Non-Walk-In complaints take product/serial/contract **only from the customer's AMC contracts** (`getallhtml`, `complaint.aspx.cs:1694`). A complaint without a contract shows no product unless it is **Walk In**, where product + serial sit on the call itself (`a.productcode`, `a.serialno`).
- Visits → **`callvisits`**; spares → **`amcusedproduct`**; neither touches `stocktrans` / `productstocksummary` (verified: no stock code in `complaint.aspx.cs`).
- Complaint No (`pmscomplaintno`) is a **read-only, auto-number** field for saksham; `autonumber` row "AMC Complaint" has `allowautogenration = 'N'`, so migrated numbers stay as written.

Form fields visible for saksham (all company gates in `default.asp` resolved — saksham is not named in any of them):
Complaint Date, Customer, Branch, Contact Person, Serial Number, Complaint, Assigned To, PMS Date, Contract Number, Status, Accompanied By, Report No, Complaint No (read-only), Call Amount, Visit Date, Visit Time, Service Complaint, Observation, Service Details, Test Result, Remark, Payment Terms.
Not visible for saksham (other clients' fields): Department, Installed At/On/By, Principle Name, Assigned Role, Type Of Complaint/Job, Product Make/Description, Job Carried Out, AMC Visit, Completion Date/Time, Order Confirmation No., Installation Remark, Contact-person name/mobile/email, Address, Faulty Inward / OEM tabs, Task tab, View Quotation/Invoice/Expense tabs.

---

## 3) Header: `trhcalls` → `contractcall`

| Client field (Service Entry) | Client column | Our column | Note |
|---|---|---|---|
| Unique Call No. | `vtrnno` | `pmscomplaintno` | e.g. `26E30007`; read-only "Complaint No" on the form |
| Trn. No. | `vtrnprefix` + `ntrnno` | `reportno` | e.g. `CL16505`; "Report No" field |
| Date | `dtrndate` | `date` **and** `complaintdate` | list filters on `date` |
| Call Type | `ncalltype` | `type` + label | see §4 |
| Party | `nparty` | `ccode` | party → contact map (module 1) |
| Contact Person | `npartycontact` (28,118) | `contactperson` | `mltcontact` by party + name |
| (party branch) | — | `contactbranch` | party's `mltaddress` (same rule as Sales Invoice Bill To) |
| Item / Serial No. | `nitem`, `vserialno`, `nitemserialno` | `productcode`, `serialno`, `contractdtcode` | contract line resolved by serial + call date (§5) |
| Our Complaint | `ncomplaint` (28,726) | `complaint` → `complainttype` | master seeded from `mstcomplaint` (100 rows) |
| Complaint Reported | `vcomplaint` (36,164) | `servicecomplaint` | "Service Complaint" textarea |
| Special Instructions | `vspecialinstructions` (1,513) | `Observation` | |
| Solve Remarks | `vsolveremarks` (6,653) | `servicedetails` | "Service Details" |
| Service Person | `nengineer` (32,691) | `assignedto` | user map |
| Appointment Date | `dapptdatetime` (22,058) | `preferredtimein` | "Visit Date / Visit Time" |
| Solved + date | `bsolved`, `dsolvedatetime` (31,279) | `status`, `closedt` | §4 |
| Total Amount | `ntotalamount` (1,240 ≠ 0) | `callamt` | "Call Amount" |
| Location | `vlocation` (2,592) | `remark` line | no separate column on the form |
| Warranty months / Item Status W-C-O | `nwarrmonth` (36,080) | label (§4) + `remark` line | |
| Comment | `vcomment` (853) | `remark` line | |
| Person Calling | `vpersoncalling` (325) | `remark` line | |
| Allocation Date | `dallocationdatetime` (32,654) | `remark` line | no column in our table |
| Transfer To (office) | `nTransferTo` (15,291) | `remark` line | our branch scoping comes from the **contact**, not the call |
| Pending / Cancel reason | `npendingreason` (3,117), `ncancelreason` (4,767) | `remark` line + status | names from `mstcallpendingreasons` (129) / `mstcallcancelreasons` (26) |
| Created / edited | `addedby`, `addedon`, `editedby`, `editedon` | `createdby`/`createdon`, `updatedby`/`updatedon` | migration user, real dates |
| — | — | `type`, `autogen` | `autogen` left NULL (auto-number is OFF) |

Not carried (empty or no place in our form): `vmanualjobno` (0), `nestimateamt` (0), `vapprovedby` (0), `vtcrno` (0), `ntechtype` (0), `nescalateto` (0), `nfollowupno` (0), `bdelivered` (0), `nsoldby` (0), `ncheckset` → §8, invoice no/date (983 — the call's own bill, see §7).

---

## 4) Type, status and labels

**Type** (`contractcall.type` — only the 4 app values may be used):

| Client call type | Our `type` |
|---|---|
| P M VISIT | `PMS` |
| INSTALLATION CALL | `Installation` |
| Breakdown / Courtesy / Demo / Validation / Inspection / Paid Service / Others | `Complaint`, or `Walk In` when the serial has no contract (§5) |

The **exact eBizWiz call type** is kept as a **label** (`label` category `amc` + `labelrelation`) — the same mechanism used for Warranty / Non-Warranty on Sales Invoice, and the Labels button already exists on the complaint list. 9 labels, one per client call type. Warranty status (`bwvoid`, `nwarrmonth`) can use the existing `Warranty` / `Non Warranty` labels.

**Status** (`status`, module `AMC` — today only Open/Pending and Closed/Completed, both created by module 10):

| Client state | Calls | Our status |
|---|---|---|
| `bsolved = 1` | 31,169 | **Closed** (Completed) + `closedt` = `dsolvedatetime` |
| cancel reason set, not solved | 4,767 | **Cancelled** — new row in `status` (module AMC, behavior Completed) |
| neither | 228 | **Open** (Pending) |

Pending reason (3,117 calls) is written into `remark`, since a call can be solved *and* carry a pending reason — it is not a status by itself.

---

## 5) Contract / product resolution

1. `nitemserialno` → contract lines `trdcontr2itemsdet` with the same serial → pick the line whose `dstartdate … denddate` covers the call date (**17,226** calls), else the latest line that starts before the call date (up to 23,027).
2. That line → our `contractdetails.code` → `contractdtcode`, and `ccode` / `productcode` / `serialno` come from the contract (exactly what `getallhtml` expects).
3. No contract line for the serial → **`Walk In`**: `ccode` = party, `productcode` = `nitem` product, `serialno` = `vserialno`, `contractdtcode` = 0. This is the only shape in which our form shows a product that is not under contract.

**PM VISIT calls that are already migrated:** module 10 created 14,219 `type='PMS'` rows from `trdcontr7pmvisit`. 12,930 of those PM visits point to a call (`ncalls`). Those calls **do not create a second row** — the existing PMS row is updated (engineer, appointment, solve date/status, remarks, complaint no., report no.) and its visits/spares hang off it. Matching key: `contractdtcode` + scheduled date (`contractcall.date` = `dschpmdate`). The remaining 4,251 PM VISIT calls are new rows (`PMS` when the serial has a contract, otherwise `Walk In`).

Expected new rows: 36,164 − 12,930 = **23,234** new `contractcall` rows + 12,930 updated.

---

## 6) Visits → `callvisits`, faults, spares

**Visits** (`trdcalls1visit`, 56,248) → one `callvisits` row each:

| Client | Ours |
|---|---|
| `dvisitdatetime` | `date` |
| `nvisitby` | `assignedto` |
| `vvisitremark` (55,111) + `vcustomerRemarks` (4,329) + `vPartsReplacedDetails` (1,321) + `vVisitTrnNo` + `ntimespent` | `remark` |
| call's complaint type | `complaint` |
| visit's fault `bsolve` | `status` (Closed / Open) |
| call's `ccode`, `productcode`, `type`, `pmscomplaintno` | same columns (the app copies them onto the visit row) |

**Faults** (`trdcalls2fault`, 32,522) — our project has no fault table; each fault is one line appended to its visit's `remark`:
`Complaint: <mstcomplaint> | Defect: <mstdefect> | Repair: <mstrepair> | <vremarks>` (49 defects, 45 repairs).

**Spares** (`trdcalls3parts`, 6,858) → `amcusedproduct` (the "Used Product" tab, visible for saksham):

| Client | Ours |
|---|---|
| `nitem` | `pcode` |
| `nquantity` (plain units: 6,889 rows = 1) | `qty` |
| product unit | `unit` |
| visit engineer | `executive` |
| `nrate` (1,281 ≠ 0) | `quotationcost` |
| `vsparelocation`, `ndefqty`, `bclaimed`, `vremarks` | `remark` |
| call | `complaintcode` |

---

## 7) Decisions needed before the exe is written

| # | Question | Options | Recommended |
|---|---|---|---|
| **D1** | **Stock for spares.** In eBizWiz the parts used on calls reduce stock (`stkouInWarranty` / `stkouOutWarranty` in the stock report); no Stock-OUT document is linked to a call (`trhstkou.ncalls` = 0 on all 1,403). Our complaint module never touches stock (verified in code), so `amcusedproduct` alone leaves our stock **6,858 pieces higher** than the client's. | (a) also post an **MRO** (Stock-OUT) per call with spares → stock matches the client and the movement is visible in Inventory; (b) only ledger rows without a document; (c) leave stock untouched | **(a)** — same pattern as module 8 (D3 SAL), stock stays verifiable |
| **D2** | **Call type "Cancelled".** 4,767 cancelled calls. | (a) new `status` row **Cancelled** (module AMC, behavior Completed); (b) mark them Closed and put "Cancelled" in the remark | **(a)** — real client data, the app reads `status` by behavior |
| **D3** | **Calls with no contract** (about 13,000). | (a) `Walk In` (product + serial visible); (b) `Complaint` with `contractdtcode = 0` (product blank on the form) | **(a)** |
| **D4** | **Check lists** (224,583 rows, `trdcalls8checks` + `mstchecks` 251 / `mstcheckset` 34). Our complaint check-list modal is hard-coded to other clients' templates (DHL / DB Schenker / Sony & ITM / Zepto / Service Report) and the standard Task-checklist tab is Zinq/Skytech/Cona-only. | (a) skip; (b) migrate into `taskchecklist` (the standard component used for CQ/SO) + **one-line form change** to show the checklist tab for saksham; (c) append into the call remark | **(b)** if a form change is allowed, otherwise **(a)** — 225k rows in a remark is not usable |
| **D5** | **Complaint type master.** `complainttype` has 2 demo rows; the client has 100 (`mstcomplaint`). | insert the 100 client values | insert (real data, same as earlier modules) |
| **D6** | **Labels** for call type / warranty (§4). | create 9 + 2 labels under category `amc` | create |

---

## 8) Not part of this module

| Client data | Why |
|---|---|
| `trdcalls5happy` (41,656) | This is the **Customer Feedback / Survey** menu (15 questions / 12 answers). Our `customerfeedback` holds one rating + remark per record — a separate module, to be decided with its own mapping |
| `trdcalls4outrp`, `trdcalls7standbyreplace`, `trdcalls9checksvisits`, `trdcalls9stbymissing` | 0 rows |
| `trdcalls6posttaxchgs` (8), call invoice fields (983) | Service billing out of a call; our complaint has only `callamt`. Amount is kept, the tax breakup is not |
| `trdcalls10pendingparts` (5) | 5 rows; goes into the call remark |
| Calls of offices 1 and 5 (280) | WinMax test offices, excluded everywhere in this migration |

---

## 9) Form changes

**None required** for D1–D3, D5, D6 — the fields used (Complaint No, Report No, Service Complaint, Observation, Service Details, Call Amount, Visit Date, Status, Assigned To, Used Product tab, Labels) are all already visible for saksham.
**Only D4 option (b)** needs a one-line change (show the standard checklist tab), exactly like the AMC Quotation checklist change in `inquiry.js`.

---

_Prepared 2026-09-16 from `saksham70v1_1`, `SakshamRMtP15329` and the project code (`amc/complaint/default.asp`, `amc/app/complaint.aspx.cs`, `amc/scripts/complaint.js`). Awaiting confirmation of D1–D6 before the exe module is written._
