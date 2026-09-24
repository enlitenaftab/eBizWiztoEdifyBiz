# 08. AMC Quotation Entry → Customer Quotation (AMC)

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Sales → Contracts → AMC Quotation** (`Contracts/amc.aspx`, "AMC Quotation Entry") — header, *AMC Quotation Items* (View Sr. No. = serial rows), **More** menu: Items, Post Tax Charges, Payment Schedule, Terms & Cond., Check List, Upload Doc | **Inquiry** module, document type `CQ` marked `modulename = 'AMC Contract'` — same quotation form / view as module 4 (Quotation Details, Product Details + Adjustments + Term Details, Remarks, Task Check List) |
| Tables | `trhoffer` (header), `trdoffer1items` (product lines), `trdoffer2itemsdet` (serial rows: rate, period, PM visits), `trdoffer3posttaxchgs` (charges), `trdoffer5tempbills` (payment schedule bills), `trdoffer6checks` (check list); masters `mstfixedselection` (Offer Type, Offer Is, Conversion, contract type), `mstpaymentschedule`, `msttermset`, `mstcheckset` + `msdcheckset`, `mstchecks`, `mstprepostchgs`, `msttaxset` | `inqcs` (`cors = 'CQ'`, `modulename = 'AMC Contract'`), `inqcsdet` (one per serial), `inq_adjust`, `taskchecklist` (`module = 'CQ'`); masters `miscellaneous` (Inquiry / Quote Type), `status` (Quotation), Checklist Master `taskchecklistmaster` + `taskchecklistmasterdet` |
| Code | — | `inquiry/default.asp`, `assets/scripts/inquiry.js`, `app/inquiry.asp` |
| Exe | — | module **9. AMC Quotation** — class `AmcQuotation` in `Program.cs` (after modules 1–8) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) — 8,758 quotations | |
| Status | | migrated and verified |

EdifyBiz has no separate AMC quotation screen; the AMC contract links to a **Customer Quotation** (`amc.module = 'CQ'`, module 10), so the AMC quotation is a CQ marked "AMC Contract".

---

## 2. Field mapping

### 2.1 Header → `inqcs`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `trhoffer.vtrnprefix` + `ntrnno` | `inqcs.inqref` | Quotation Reference | page title | `QA…` (sales quotations are `QT…`) |
| 2 | Trn. Date | `dtrndate` | `inqcs.inqdate` | Quotation Date | Quotation Date | |
| 3 | Party | `nparty` → `mstparty` | `inqcs.cscode` | Customer | Customer Name | party → contact (module 1) |
| 4 | (Bill To) | the party's default address | `inqcs.csbranch` | Customer Billing Branch | Customer Billing Branch | shown on the view as party - place - address |
| 5 | Party Contact | `npartycontact` → `msdparty` | `inqcs.cperson` | Contact Person | Contact Person | `mltcontact.code` |
| 6 | Sales Person | `nsalesman` → `mstusers` | `inqcs.executive` | Executive | Executive Name | user by name; Admin when blank |
| 7 | Follow Up Date | `dfollowupdate` | `inqcs.followupdate` | Follow Up Date | Follow Up Date | 652 |
| 8 | Offer Type | `noffertype` → `mstfixedselection` | `inqcs.quotetype` | Quote Type | Quote Type | "AMC - FROM CONTRACT / FROM WARRANTY / FROM CALLS / FRESH" in the Quote Type master |
| 9 | Conversion | `nconversion` → `mstfixedselection` | part of `inqcs.remark` | Remarks | Remarks tab | `Conversion chance: MAY BE` (790) |
| 10 | Pymt. Schedule + B / E of Period | `npaymentschedule` → `mstpaymentschedule`, `vbegorend` | part of `inqcs.terms` | Terms | Term Details | first part: `Payment Schedule: Half Yearly - Beginning of Period` (all 8,758) |
| 11 | Payment Schedule (bill dates / amounts) | `trdoffer5tempbills` | part of `inqcs.terms` | Terms | Term Details | after the schedule: `Schedule Bills: dd/MM/yyyy = amount` (1 row: office 2, offer 4414 → `Schedule Bills: 31/03/2027 = 40887.00`) |
| 12 | Terms & Cond. | `nterms` → `msttermset` (+ text `vtermsconditions`) | part of `inqcs.terms` | Terms | Term Details | `Terms & Cond.: ESCO Standard Terms and Conditions` (1,436), then the text (829); parts joined with ` \| ` |
| 13 | Check List | `ncheckset` → `mstcheckset` | — | — | — | the set is seeded as a Checklist Master (§3); its name is not written into the remark; the items are in 2.4 |
| 14 | Ref. No. / Ref. Date | `vrefno`, `drefdate` | `inqcs.enqrefno`, `enqrefdate` | Enquiry/Tender Ref No / Ref Date | Ref No / Ref Date | 6,560 / 6,169 |
| 15 | Remarks / Comment | `vremarks`, `vcomment` | `inqcs.remark` | Remarks | Remarks tab | `Comment: …` |
| 16 | Trn. Total | `ntotalamount` | — | — | Grand Total | computed (2.3) |
| 17 | Select Letterhead to print | `vletterhead` | — | — | — | print option |
| 18 | (AMC marker) | — | `inqcs.modulename = 'AMC Contract'` | — | — | standard "Quotation For" value |
| 19 | (approval) | `bapproval` (1 on all) | `inqcs.approvalstatus = 'Approved'` | — | — | Print / Documents need it (04 §7) |
| 20 | (outcome) | serial `nnewcontractno` / `bclosed` | `inqcs.status` | Status | Status | AMC - Contract Signed / Partly Contract Signed / Closed / Open |
| 21 | (office) | `nofficeid` | `inqcs.branchcode`, `comcode` | Branch / Company | Company | branch 3–6 |
| 22 | — | — | `inqcs.currency` | Currency | Currency | INR (the client has no currency) |

### 2.2 AMC Quotation Items → `inqcsdet` (one line per serial)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 23 | Item / Item Code | `trdoffer1items.nitem` | `inqcsdet.pcode` | Product | Product Name | product by Item Code (module 2, `product.casno`) |
| 24 | Quantity | — | `inqcsdet.quantity` = 1 | Quantity | Quantity | one line per serial; a product line without serial rows keeps its quantity (6 lines) |
| 25 | (serial) Master Rate / Discount % / Rate | `trdoffer2itemsdet.nmasterrate`, `ndiscountperc`, `nrate` | `inqcsdet.price`, `discount`, `discountpercent` | Price / Discount | Price, Discount Price | discounted → price = master, discount = master − rate; else price = rate |
| 26 | (serial) Serial No. | `vserialno` | `inqcsdet.prodsrno` + Product Description | — / Product Description | Product Name (below) | `S/N: …` |
| 27 | (serial) Location | `vlocation` | `inqcsdet.location` + Product Description | — / Product Description | Product Name (below) | `Location: …` |
| 28 | (serial) contract type, start – end, months, PM visits, old end date, first installation date | `ncontrtype`, `dstartdate`, `denddate`, `nmonths`, `npmvisits`, `doldenddate`, `dfirstinstdate` | `inqcsdet.pdesc` | Product Description | Product Name (below) | labelled lines: `Contract Type: …`, `Period: 01/04/2025 - 31/03/2026 (12 months)`, `PM Visits: 2`, `Old End Date: …`, `First Installed: …` (no install field on a quotation line) |
| 29 | Offer Is + previous document | `trdoffer1items.nofferis`, `nprevnumber` | `inqcsdet.pdesc` | Product Description | Product Name (below) | `CONTRACT RENEWAL`, `Previous Contract: MC…` / `Sales Invoice: SA…` |
| 30 | (serial outcome) | `nnewcontractno`, `bclosed` | `inqcsdet.pdesc` | Product Description | Product Name (below) | `Contract: MC…` or `Closed` |
| 31 | Tax Set | `ntaxset`, `ntaxamt` | `inqcsdet.prodtax` | Tax % | — | quotation GST % (2.3), else Tax Set % |
| 32 | Technical Set | `trdoffer4tech` | — | — | — | 0 rows |

### 2.3 Post Tax Charges → `inq_adjust`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 33 | Post Tax Charges | `trdoffer3posttaxchgs` → `mstprepostchgs` | `inq_adjust` (`adjustname`, `adjustpercent`, `adjustamount`) | Adjustments row | Adjustments | PERCENTAGE → item total (Σ serial `nrate`) × %; AMOUNT as entered; `Tax on items (eBizWiz)` / `Other items (eBizWiz)` keep Grand Total = client total — same rules as module 4 |
| 33a | Created By / Updated By | `addedby` / `editedby` → `mstusers` | `inqcs.createdby` / `updatedby` | — | Created By / Updated By | not edited → creator; unknown user → migration user. verified in the DB on 23/09/2026 |

### 2.4 Check List → `taskchecklist` (`module = 'CQ'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 34 | Check, Done / Done Date, Expected Date, Alloted To, Remarks | `trdoffer6checks` (+ `mstchecks`) | `taskchecklist` | Task Check List tab | Task Check List tab | `modulecode` = quotation code; same rules as module 4 |

---

## 3. Masters seeded

| Master | Our table | Rows added | Source |
|---|---|---|---|
| Quote Type | `miscellaneous` (Inquiry / Quote Type) | 4: AMC - FRESH / FROM WARRANTY / FROM CONTRACT / FROM CALLS | `mstfixedselection` `noffertype` (full group) |
| Quotation Status | `status` (module Quotation) | 4: AMC - Contract Signed / Partly Contract Signed / Closed / Open | outcome of the serial rows |
| Checklist Master | `taskchecklistmaster` + `taskchecklistmasterdet` (title, days, sort) | 24 sets / 253 items — shared by all modules with a check list, seeded once, idempotent by title | `mstcheckset` + `msdcheckset` (+ `mstchecks` names, `ndaysdiff`): sets of offices 2/3/4/6 that are active or used by a migrated document |

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | Quotation view | Task Check List tab shown for CQ (tag = document type) — shared with module 4 | Behaviour | `assets/scripts/inquiry.js` | `taskchecklist` |
| 2 | Quotation view | **Customer Billing Branch** row: party - place - address of `inqcs.csbranch` — shared with CI / CQ / SO (see 03) | Layout | `inquiry/default.asp`, `app/inquiry.asp`, `assets/scripts/inquiry.js` | `inqcs.csbranch` |
| 3 | Quotation form | Customer Branch labelled **Customer Billing Branch** (same label as the Sales Invoice form) — shared with CI / CQ / SO (see 03) | Label | `inquiry/default.asp`, `assets/scripts/inquiry.js` | `inqcs.csbranch` |

Originals are kept as comments ("- Aftab Alam"). The Quotation Details fields (Quote Type, Loss Reason, Ref No / Date, Follow Up Date, Category) are the ones of module 4.

**New fields: 0** on the form · view rows: 1 (Customer Billing Branch, shared with 03).

### 4b. Database changes (ALTER)

None beyond module 4 (`quotetype`, `enqrefno`, `enqrefdate`, `followupdate`).

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Header discount | `trhoffer.ndiscount` | 1,711 | information only (= Σ master − rate), never deducted |
| Serial approved flag / date, manufacturer | `bapproved`, `dapprovedate`, `nmanufacturer` | 6,287 / 6,287 / 8 | the approval is the contract link (`nnewcontractno`, row 30) |
| Dispatch fields | `ndispatchmode`, `vdespatchthru`, `vdespdocno`, `ddespdocdate` | 0 | empty |
| Technical Set | `trdoffer4tech` | 0 | empty |
| Upload Doc | `trhdoc` | — | DMS is a separate round |

---

## 6. Verification (latest run)

Run ALL, 0 errors:

| Check | Client | Ours |
|---|---|---|
| Quotations | 8,758 | **8,758** `inqcs` CQ `modulename = 'AMC Contract'`, all Approved, currency INR |
| Contact person | 8,069 set, 38 point to a blank / missing client person | **8,031** |
| Sales person / Follow Up / Quote Type | 8,158 / 652 / 8,755 | same |
| Ref No / Ref Date / Conversion | 6,560 / 6,169 / 790 | same |
| Terms | Payment Schedule 8,758, term set 1,436, text 829 | `inqcs.terms` on **8,758**, all starting `Payment Schedule:`; `Terms & Cond.:` on **1,436**; **0** remarks holding `Payment Schedule:` / `Terms & Cond.:` |
| Lines | 13,535 serial rows + 6 product lines without serials | **13,541** `inqcsdet` (0 skipped); serial no. 13,517 / location 9,691 = client |
| First installation | 6,343 serials | `First Installed:` in **6,343** line descriptions (`inqcsdet.pdesc`) |
| Status | — | Contract Signed 3,999 · Closed 4,362 · Partly Contract Signed 126 · Open 271 |
| Check list | 25,833 items | **25,833** `taskchecklist` CQ |
| Charges | 8,714 quotations with a client total | total = client on 8,696 (the rest: client totals that don't follow their own charges) |

---

## 7. Notes

- **Why a Customer Quotation.** The standard AMC contract links to a quotation with `amc.module = 'CQ'` + `modulecode`, and the quotation's "Quotation For" value `AMC Contract` exists for this purpose. Module 10 uses that link (4,165 contracts).
- **One line per serial** because rate, period and PM visits sit on the serial row, and the AMC contract line (`contractdetails`) is per serial too.
- **Serial detail in Product Description.** The saksham quotation view shows only the product name and its description; `prodsrno` / `location` columns are filled as well but are not displayed for saksham.
- **Terms.** Payment Schedule, B / E of Period, the schedule bills and the Terms & Cond. set name go to the Terms field — the same place the sales quotation, sales order, PO and invoice use for payment terms — never to Remarks.
- **Check lists.** The check-set name is not written into any remark: the set with its items is the standard Checklist Master, and the quotation's own check items are in the Task Check List tab.
- **Status values** are derived: every serial converted → Contract Signed, some → Partly, none but closed → Closed, else Open.
- **Charges** use the formula of module 4 (PERCENTAGE on the item total), which matches the client total on 8,696 of 8,714 quotations.
