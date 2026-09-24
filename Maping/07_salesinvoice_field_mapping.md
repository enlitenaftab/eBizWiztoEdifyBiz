# 07. Product Sale Entry → Sales Invoice

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Product Sale Entry** — Warranty Sales and Non Warranty Sales ("Spare Part Sale Entry") menus; header, *Sales Items* (with View Serial No.), **More** menu: Post Tax Charges, Payment Terms, Terms & Cond., Check List, Upload Doc | **GST Sales → Sales Invoice** (`/edify/gst/sales/`, type `SO`) — form, view, Product Details (lines + GST + Adjustments), Labels, Task Check List tab |
| Tables | `trhsales` (header), `trdsales1items` (items), `trdsales2itemsdet` (serial / warranty per item), `trdsales3payterms` (payment terms), `trdsales4posttaxchgs` (GST + charges), `trdsales6checks` (check list), `trdsales7pmvisit` (warranty PM visit schedule); masters `mstfixedselection` (Sales Type, Dispatch Mode), `mstinquirysource`, `mstpaymentterms`, `msttermset`, `mstcheckset` + `msdcheckset`, `mstchecks`, `mstprepostchgs`, `msttaxset` | `sal_order` (`type = 'SO'`), `sal_order_det`, `sal_tax`, `sal_adjust`, `labelrelation` (label category `SAL`), `taskchecklist` (`module = 'SAL'`); masters `miscellaneous` (Sales / Sales Type, Sales / Dispatch Mode), `label`, `state` (GST codes), `companyaddress` (GST city), Checklist Master (`taskchecklistmaster` / `taskchecklistmasterdet`). `sal_order` also holds the AMC contract bill invoices (module 10, label "AMC Contract Bill") and the service call bill invoices (module 11, label "Service Call Bill") — see 13 |
| Code | — | form + view `gst/sales/default.asp`, JS `gst/sales/script/sales.js`, backend `gst/sales/app/sales.asp` (save by field name through `SqlOperation`) |
| Exe | — | module **7. Sales Invoice** — class `SalesInvoice` in `Program.cs` (after modules 1–6; GST setup `Shared.EnsureGstSetup`) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) — 5,707 invoices | |
| Status | | migrated and verified |

---

## 2. Field mapping

### 2.1 Header → `sal_order` (`type = 'SO'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `trhsales.vtrnprefix` + `ntrnno` | `sal_order.salorderno` | Invoice No. | page title | e.g. `SA1879` |
| 2 | Trn. Date | `dtrndate` | `sal_order.salorderdt` | Invoice Date | Sales Date | |
| 3 | Party | `nparty` → `mstparty` | `sal_order.ccode` | Customer | Customer Name | party → contact (module 1) |
| 4 | Party Contact | `npartycontact` → `msdparty` | `sal_order.contactperson` | Contact Person | Person Name | `mltcontact.code` |
| 5 | Sales Type | `nsalestype` → `mstfixedselection` | `sal_order.ordertype` | **Sales Type** ✚ | **Sales Type** ✚ | name as text; full client group seeded |
| 6 | Sales Person | `nsalesman` → `mstusers` | `sal_order.executive` | Executive | Executive Name | user by name; Admin when not found |
| 7 | Bill To | `nbillto` → `mstparty` → its contact's address | `sal_order.cbranchcode` | Customer Billing Branch | **Customer Billing Branch** ✎ | free-hand branch picker; **blank → the party's own branch** (needed for the GST state); view shows party - place |
| 8 | Ship To | `nshipto` → `mstparty` → its contact's address | `sal_order.shippingbranch` | Customer Shipping Branch | Customer Shipping Branch | free-hand branch picker; view shows party - place |
| 9 | Campaign | `ntrhcampa` | — | — | — | 0 invoices |
| 10 | Sales Source | `nsalessource` → `mstinquirysource` | part of `sal_order.remarks` | **Remarks** ✚ | **Remarks** ✚ | `Sales Source: Service Visit` (113) |
| 11 | P.O. No. | `vpono` | `sal_order.ponumber` | **P.O. No.** ✚ | **P.O. No.** ✚ | |
| 12 | P.O. Date | `dpodate` | `sal_order.purorderdt` | **P.O. Date** ✚ | **P.O. Date** ✚ | |
| 13 | Dispatch Mode | `ndispatchmode` → `mstfixedselection` | `sal_order.dispatchmode` | **Dispatch Mode** ✚ | **Dispatch Mode** ✚ | name as text; `miscellaneous` Sales / Dispatch Mode seeded (full group) |
| 14 | Dispatch Through | `vdespatchthru` | `sal_order.couriername` | **Dispatch Through** ✚ | **Dispatch Through** ✚ | |
| 15 | Docket No. | `vdespdocno` | `sal_order.courierno` | **Docket No.** ✚ | **Docket No.** ✚ | |
| 16 | Docket Date | `ddespdocdate` | `sal_order.dispatchdate` | **Docket Date** ✚ | **Docket Date** ✚ | |
| 17 | Terms & Cond. | `nterms` → `msttermset` | part of `sal_order.term` | Terms | Terms | `Terms & Cond.: ESCO Standard Terms and Conditions` (49) |
| 18 | Check List | `ncheckset` → `mstcheckset` | — (the set is a Checklist Master, §3) | — | — | the set name is not written into any remark; the invoice's check items are in 2.6 |
| 19 | Ref. No. / Ref. Date | `vrefno`, `drefdate` | part of `sal_order.remarks` | **Remarks** ✚ | **Remarks** ✚ | `Ref No: …`, `Ref Date: …` |
| 20 | Remarks | `vremarks` | `sal_order.remarks` (first part) | **Remarks** ✚ | **Remarks** ✚ | |
| 21 | Comment | `vcomment` | part of `sal_order.remarks` | **Remarks** ✚ | **Remarks** ✚ | `Comment: …` |
| 22 | Demo | `bdemo` | part of `sal_order.remarks` | **Remarks** ✚ | **Remarks** ✚ | `Demo: Yes` (15) |
| 23 | Trn. Total | `ntotalamount` | part of `sal_order.remarks` | **Remarks** ✚ | Grand Total / **Remarks** ✚ | our total is computed (2.4); the client total is kept as `eBizWiz Invoice Total: …` |
| 24 | (amount received) | `namountrecd` | part of `sal_order.remarks` | **Remarks** ✚ | **Remarks** ✚ | `Amount Received: …` (678); receipts themselves are module 12 |
| 25 | (against quotation / order) | `nquote`, `nordrc` | `sal_order.inqcsso` | Quotation/Sales Order No. | Quotation/Sales Order No (link) | the migrated SO, else the migrated quotation |
| 26 | (Warranty Sales / Non Warranty Sales menu) | `bwarranty` | `labelrelation` (label category `SAL`) | Labels | Labels | "Warranty Sales" (4,313) / "Non Warranty Sales" (1,394); the same label category carries "AMC Contract Bill" / "Service Call Bill" on the bill invoices of modules 10 / 11 |
| 27 | Select Letterhead to print | `vletterhead` | — | — | — | print option |
| 28 | (office of the record) | `nofficeid` | `sal_order.branchcode`, `comcode` | Branch Name / Company Name | Company Branch | office → `companyaddress.code`; company 1 |
| 29 | — | — | `sal_order.currency` | Currency | Currency | INR (the client invoice has no currency) |
| 30 | — | `addedon` / `editedon`, `addedby` / `editedby` | `createdon` / `updatedon`, `createdby` / `updatedby`  — | Created By / Updated By | `createdby` / `updatedby` = the eBizWiz `addedby` / `editedby` user (not edited → creator; unknown user → migration user). verified in the DB on 23/09/2026 |

### 2.2 Sales Items → `sal_order_det`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 31 | Item / Item Code | `trdsales1items.nitem` → `mstitems` | `sal_order_det.prodcode` | Product Name | Particulars | product by Item Code (module 2, `product.casno`); `prodbatchcode` = the product's Default Batch |
| 32 | Quantity | `nquantity` | `sal_order_det.qty` | Quantity | Quantity/Unit | |
| 33 | Master Rate / Discount % / Discount Amt. / Warr. Sale Rate (WS Rate) | `nmasterrate`, `ndiscountperc`, `ndiscount`, `nrate` | line with a discount %: `sal_order_det.price` = `nmasterrate`, `discount` = qty × master × % (net = qty × `nrate`); otherwise `price` = `nrate` | Price (Per Unit), Discount | Price/Unit, Discount | **Changed 23/09/2026 (verified in the DB 24/09):** the client screen shows Rate + Discount %, so the master rate and its discount amount are kept (verified: on all 9,967 discounted lines master × (1 − %) = `nrate`, so the taxable value and GST do not change). `ndiscount` is never deducted in eBizWiz; a pre-GST invoice discount is still spread by the exe (2.4) |
| 34 | Tax Set | `ntaxset` → `msttaxset` | `sal_order_det.gst` | GST (%) | CGST / SGST / IGST rate | invoice GST % (2.4); else Tax Set % (29 lines) |
| 35 | Technical Set | `ntechnicalset` | — | — | — | 0 lines |
| 36 | Main Item / Auto Generat Dummy Serial No. | `bmainitem`, `bautoserial` | — | — | — | 5,353 / 2,634 lines; entry helpers for bundles and dummy serials, no field on our line |
| 37 | Item Total | — | — | — | Taxable Amount / Total | computed |

### 2.3 View Serial No. → `sal_order_det` (same line)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 38 | Serial No. | `trdsales2itemsdet.vserialno` (per line via `nsales1`) | `sal_order_det.installedremarks` | — | — | `Serial No: a, b, c` (nvarchar(1000), §4b) |
| 39 | Warranty months | `nmonths` | `sal_order_det.warrantymonths` | — | — | the Warranty pop-up is medispec-only |
| 40 | Installation Date / Location | `dinstdate`, `vlocation` | `sal_order_det.installationdt`, `location` | — | — | |
| 41 | Serial, warranty start–end + months, PM visits, installed, location, closed | `vserialno`, `dstartdate`, `denddate`, `nmonths`, `npmvisits`, `dinstdate`, `vlocation`, `bclosed` | `sal_order_det.prod_desc` | Product Description | Particulars (below the name) | **visible copy**, one line per serial: `S/N: 12345, Warranty: 01/04/2025 - 31/03/2026 (12 months), PM Visits: 2, Installed: 05/04/2025, Location: Lab 2` |

### 2.4 GST + Post Tax Charges → `sal_tax` / `sal_adjust`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 42 | Post Tax Charges — GST lines ("Add : IGST @18%", CGST + SGST, GST x %) | `trdsales4posttaxchgs` → `mstprepostchgs` | `sal_order_det.gst` + `sal_tax` (per product: CGST/SGST or IGST, `taxpercent`, `taxamount`) | GST (%) | CGST / SGST / IGST columns, Total GST | main GST charge → % on every line; CGST+SGST vs IGST follows the view's state rule (branch state 27 = Bill To state) |
| 43 | Post Tax Charges — other charges (freight, packing, insurance, discount, round off, "TOTAL …" rows with an amount) | same | `sal_adjust` (`adjustname`, `adjustpercent`, `adjustamount`) | Adjustments row | Adjustments | PERCENTAGE → item total × %; AMOUNT as entered; zero skipped |
| 44 | GST the lines can't carry | — | `sal_adjust` | Adjustments row | Adjustments | `GST on charges (eBizWiz)` / `GST rounding (eBizWiz)` / `GST difference (eBizWiz)` |
| 44a | (GST rounding) | — | `sal_tax.taxamount` | — | CGST / SGST / IGST Amt | **Changed 23/09/2026 (verified in the DB 24/09):** the stored tax is rounded exactly as the Sales Invoice view rounds it (the view works in JavaScript doubles), so the view Grand Total and the Outstanding agree. The stored tax is also the sum of the per-line rounded amounts, because the view adds up line by line. After the 24/09 run an exact .005 midpoint matches the view (SA2447 stores 4,592.20 per tax row), and 244 of 6,512 invoices still differ by 1-2 paise where JavaScript and .NET land on opposite sides of a midpoint - the eBizWiz total itself is always kept through the GST rounding / difference adjustment. Any remaining gap to the eBizWiz total stays in the `GST rounding (eBizWiz)` adjustment |
| 45 | Pre-GST discount (GST charged on items − an earlier AMOUNT discount) | — | `sal_order_det.discount` | Discount | Discount | spread over the lines by value |
| 46 | Blank-item / unmapped lines | `trdsales1items` without a migrated item | `sal_adjust` | Adjustments row | Adjustments | `Other items (eBizWiz)` |

### 2.5 Payment Terms → `sal_order.term`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 47 | Payment Terms, Percentage, Amount | `trdsales3payterms.nmstpayterms` → `mstpaymentterms`, `npercentage`, `namount` | part of `sal_order.term` | Terms | Terms | `Payment Terms: Against Delivery 70% = 654647.70; Against Completion of Work 30% = 280563.30` before Terms & Cond. (1,564 rows / 1,307 invoices) |

### 2.5b Warranty + PM visit schedule → AMC contract type Warranty (built in module 10)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 47a | (warranty of the invoice) | `trhsales` with serial rows (4,262) | `amc` (`amctype` Warranty, `module` 'SAL', `modulecode` = `sal_order.code`) | AMC Contract screen | invoice view → AMC Contracts; AMC Contract list / view | contract no. = invoice no.; customer, contact, executive, Bill To, branch from the migrated invoice; period = earliest start – latest end (details: 11 §2.6) |
| 47b | View Serial No. — serial, warranty start / end, months, PM visits, install date, location | `trdsales2itemsdet` (6,937) | `contractdetails` | contract lines | contract lines | one row per serial; `intervals` = PM visits per year |
| 47c | PM visit schedule | `trdsales7pmvisit` (5,131: 2,029 done, 3,102 open, 753 of them in the future) | `contractcall` `type='PMS'` | AMC Complaint / PMS | PMS list, calendar | Open / Closed with the done date; a call on that visit (4,245) updates the row in module 11 |

### 2.6 Check List → `taskchecklist` (`module = 'SAL'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 48 | Check, Done / Done Date, Expected Date, Alloted To, Remarks | `trdsales6checks` (+ `mstchecks`) | `taskchecklist` (`title`, `status`, `updatedon`, `duedate`, `assignto`, `remark`) | Task Check List tab | Task Check List tab | `modulecode` = `sal_order.code`; same rules as the other modules (56 items) |

✚ = field added to our form / view for saksham · ✎ = standard field relabelled for saksham (§4a).

---

## 3. Masters seeded

| Master | Our table | Rows added | Source |
|---|---|---|---|
| Sales Type | `miscellaneous` (Sales / Sales Type) | 4 (full group) | `mstfixedselection` `nsalestype` |
| Dispatch Mode | `miscellaneous` (Sales / Dispatch Mode) | 7 (full group: BY AIR, BY COURIER, BY HAND, BY MAIL, BY POST, BY ROAD, BY SEA) | `mstfixedselection` `ndispatchmode` |
| Sales labels | `label` (category `SAL`) | 2 ("Warranty Sales", "Non Warranty Sales"), reused by name | — |
| GST state codes | `state.stategstcode` | 44 (only where empty) | official codes by state name |
| Branch GST city | `companyaddress.citycode` | 4 branches (HO Mumbai city) | office 2 |
| Check List sets | Checklist Master: `taskchecklistmaster` (title) / `taskchecklistmasterdet` (title, days, sort) | 24 sets / 253 items, shared by all modules (seeded once, by title) | `mstcheckset` + `msdcheckset` + `mstchecks` — sets of offices 2/3/4/6 that are active or used by a migrated document |

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | Sales form + view | Customer Billing Branch / Customer Shipping Branch as free-hand branch pickers ("party - place"); the view shows the same "party - place" | Behaviour | `gst/sales/default.asp`, `script/sales.js`, `app/sales.asp` | `cbranchcode`, `shippingbranch` |
| 2 | Sales view | Bill To row labelled **Customer Billing Branch** (standard label: Customer Branch), so form and view use the same labels | Label | `gst/sales/default.asp` | `sal_order.cbranchcode` |
| 3 | Sales form + view | **Sales Type** + quick-add | New field | `gst/sales/default.asp`, `script/sales.js` | `sal_order.ordertype` |
| 4 | Sales view | **Task Check List** tab (module `SAL`) | Behaviour | `gst/sales/default.asp`, `script/sales.js` | `taskchecklist` |
| 5 | Sales form | **P.O. No.**, **P.O. Date** (own ids `sales_form_sk_*`, so the Zinq / edifybiz inputs of the same name are untouched) | New field ×2 | `gst/sales/default.asp` | `ponumber`, `purorderdt` |
| 6 | Sales form | **Dispatch Mode** dropdown + quick-add | New field | `gst/sales/default.asp` | `dispatchmode` |
| 7 | Sales form | **Dispatch Through**, **Docket No.**, **Docket Date** | New field ×3 | `gst/sales/default.asp` | `couriername`, `courierno`, `dispatchdate` |
| 8 | Sales form | **Remarks** | New field | `gst/sales/default.asp` | `sal_order.remarks` |
| 9 | Sales view | Saksham rows: P.O. No., P.O. Date, Dispatch Mode, Dispatch Through, Docket No., Docket Date, Remarks | New field ×7 | `gst/sales/default.asp` | as above |
| 10 | Sales view + edit | fill from the sales array: positions 86, 87, 102, 100, 101, 106 (already returned) and saksham value 140 (remarks) | Behaviour | `gst/sales/script/sales.js` | — |
| 11 | Backend | `remarks` appended as array value 140 for saksham only (no other position moves) | Backend | `gst/sales/app/sales.asp` | `sal_order.remarks` |

**New fields on the form: 8** (Sales Type, P.O. No., P.O. Date, Dispatch Mode, Dispatch Through, Docket No., Docket Date, Remarks) · **on the view: 8** · labels: 1 · tabs: Task Check List · behaviour / backend: 3.

### 4b. Database changes (ALTER)

From `00_ALTERS_run_before_exe.sql`:

```sql
ALTER TABLE sal_order_det ALTER COLUMN installedremarks nvarchar(1000);
ALTER TABLE sal_adjust    ALTER COLUMN adjustname      varchar(100);
```

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| OEM warranty | `doemstartdate`, `noemmonths`, `doemenddate` | 0 | empty |
| Main Item / Auto Generat Dummy Serial No. | `bmainitem`, `bautoserial` | 5,353 / 2,634 lines | entry helpers, no field on our line |
| Received / accepted / rejected / missing qty | `ntotrecdin`, `ntotacceptin`, `ntotrejectin`, `ntotmissingin` | — | return tracking; stock is module 8 |
| Header discount / Tax Set | `ndiscount`, `ntaxset` | 1,673 / 0 | the discount is already inside the line rates; GST comes from the charges (2.4) |
| Campaign, ERP code | `ntrhcampa`, `VERPCODE` | 0 | empty |
| Terms & Cond. text | `vtermsconditions` | 0 | empty |
| Select Letterhead to print | `vletterhead` | 2,772 | print option |
| Upload Doc | `trhdoc` | — | DMS is out of scope (user's decision) |

---

## 6. Verification (latest run)

Run ALL, 0 errors.

| Check | Client | Ours |
|---|---|---|
| Invoices | 5,707 | **5,707** `sal_order` SO from module 7 (labels Warranty / Non Warranty Sales); the table holds 10,663 in total with the 3,996 "AMC Contract Bill" and 960 "Service Call Bill" invoices of modules 10 / 11 (see 13) |
| Customer | 5,707 | **5,707** resolved |
| Contact person | 3,156 | **3,150** (6 point to a person of another party in the client) |
| Lines | 18,694 with an item | **18,693** (1 test-office item) — all with the product's Default Batch |
| Header fields | date, customer, executive, Sales Type, P.O. No / Date, dispatch mode / through, docket no / date, branch, currency | 0 mismatch |
| Bill To / Ship To | 4,284 / 4,675 (+1,423 blank Bill To → party) | all to the correct party |
| Serials | 6,937 serial rows | 6,936 on the lines (1 test office); warranty months, install date, location 0 mismatch |
| Serial text under the product | 4,986 lines with serial rows | `prod_desc` on **4,985** lines (the other line's item is not migrated, see Lines) |
| Links | 3,327 invoices against a migrated SO / quotation | **3,327**, 0 wrong |
| Labels | Warranty 4,313 / Non Warranty 1,394 | same |
| Payment Terms / Terms & Cond. | 1,307 / 49 invoices, 1,346 with either | `sal_order.term`: **1,307** / **49**, **1,346** in total |
| Sales Source / Demo in remarks | 113 / 15 | **113** / **15** |
| Dispatch Mode master | 7 values | `miscellaneous` Sales / Dispatch Mode **7** |
| GST | 2,063 invoices taxed | `sal_tax` 9,476 rows, 0 duplicates; `gst` = `sal_tax` % on 18,693 of 18,693 lines; tax type vs state conflicts 0 |
| Grand total | 4,796 invoices with a client total | = client total on 4,606; the 190 others are 2017 invoices whose own total doesn't add up (e.g. SA90 items 8,77,533, no charges, total 2,510) |
| Check list | 56 | **56** `taskchecklist` SAL (39 done) |
| Check List sets | sets of offices 2/3/4/6, active or used | Checklist Master **24** sets / **253** items; `Check List:` in invoice remarks **0** |
| Warranty contracts | 4,262 invoices with serials (1 has only an unmigrated item), 6,937 serials (1 test office), 5,131 PM visits (2,029 done) | **4,261** contracts, customer / number / branch = invoice on all; **6,936** lines; **5,131** PMS: Closed 2,029, Cancelled 2,196 (their call was cancelled), Open 908 (753 in the future); **4,244** PM rows updated by their call; calls total 36,164 |
| Call → contract period | 36,164 calls | linked 29,680 / Walk In 5,750 (6 of them on the test-office item "testing 123"); **0** complaint calls outside their warranty / AMC period; Installation calls before the warranty start 2,214 (allowed: warranty starts at installation); PMS rows keep their schedule |

---

## 7. Notes

- **Saksham block.** P.O. No / Date (3,526 invoices), Dispatch Mode (4,004), Dispatch Through (871), Docket No / Date (836 / 374) and Remarks (4,779) have no standard input or view row for saksham: the existing PO Number input is Zinq-only, the dispatch / courier block is aromatherapy / Cona-only (with packed-by, weight, boxes … that saksham doesn't use), and the sales screen has no Remarks field. A saksham block with its own ids shows exactly the client's fields.
- **Bill To / Ship To labels.** Form and view use the standard Sales Invoice form labels **Customer Billing Branch** (Bill To, `cbranchcode`) and **Customer Shipping Branch** (Ship To, `shippingbranch`); the view shows each as "party - place".
- **Serial / warranty** stay in their own columns (`installedremarks`, `warrantymonths`, `installationdt`, `location`) for reports and AMC conversion (`amc/app/contract.aspx.cs` copies `installationdt` / `location` into `contractdetails`), and a readable copy goes to Product Description because none of those columns is shown on the saksham sales screen.
- **Sales Source** master is `mstinquirysource` ("Service Visit", "Sales Visit" …); joining `mstfixedselection` on the same code gives unrelated values.
- **Check lists.** The invoice's check items are in the Task Check List tab (`taskchecklist`); the eBizWiz check sets with their items are the standard Checklist Master (§3). The set name is not written into the remark.
- **Warranty = AMC contract type Warranty (standard).** EdifyBiz has no separate warranty module: a warranty is an AMC contract with type **Warranty**, linked to the invoice with `amc.module = 'SAL'` / `modulecode = sal_order.code` (shown under "AMC Contracts" on the invoice view), one `contractdetails` row per serial and one `contractcall` `PMS` row per PM visit — the same tables as an AMC contract. The app's own "Convert To AMC Contract" creates the lines but no PMS rows, so the exe writes them the way module 10 writes AMC PM visits. The warranty contract number is the invoice number (the client has none; AMC autonumber is off). Future PMS rows have no engineer in the client (`assignedto` empty), like the AMC ones — the calendar lists a PMS only once it is assigned.
- **AMC / call bill invoices.** The Sales Invoice table also holds the invoices created by modules 10 (AMC contract schedule bills, label "AMC Contract Bill", 3,996) and 11 (service call bills, label "Service Call Bill", 960); their mapping is in 13. Module 7 counts in §6 are the Warranty / Non Warranty Sales invoices only.
- **GST (why and how).** eBizWiz kept GST as post-tax charges, not per product. The exe rebuilds it as `sal_tax` per line so GST reports, invoice totals and receipts (module 12) match. All saksham offices billed on the Maharashtra GSTIN (Maharashtra customer → CGST+SGST 512 of 512, other state → IGST 1,031 of 1,046); the tax type shown follows the view's state rule. A PERCENTAGE charge is always on the item total (verified 1,603 of 1,604 invoices). **Open with the client:** Bangalore / Delhi / Kolkata have no separate GSTIN — if they do, only the branch city in Company Address changes.
- The sales screen opens on the **Proforma** tab; migrated invoices are on the **Sales Invoice** tab, and every list is filtered by the user's branches.
- **Approval.** The sales form never posts `approvalstatus`; the Approve / Reject buttons are shown only for demo / enliten / HMH, so nothing is hidden for saksham. Migrated invoices leave it NULL, like form-saved ones.
