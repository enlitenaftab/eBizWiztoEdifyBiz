# 11. Contract Sales → AMC Contract (+ bill invoices, + warranty of sold serials)

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Sales → Contracts → Contract Sales** ("Contract Entry") — header, *Contract Items* (View Sr. No. = serial rows), bills, PM visits; plus the **warranty** kept on Warranty Sales (View Serial No. + PM visit schedule) | **AMC → Contract** (`/edify/amc/contract/`) — header, equipment lines, Billing Cycle, All Complaints, All PMS, Invoice, Expense tabs; billed bills as **GST Sales → Sales Invoice** (label "AMC Contract Bill"); invoice view → "AMC Contracts" |
| Tables | `trhcontr` (header), `trdcontr1items` (product lines), `trdcontr2itemsdet` (serial rows), `trdcontr3bills` (bills), `trdcontr7pmvisit` (PM visits), `trdcontr4posttaxchgs` (charges); warranty: `trhsales`, `trdsales2itemsdet`, `trdsales7pmvisit`; masters `mstfixedselection` (contract is / type), `mstpaymentschedule`, `msttermset`, `mstcheckset` + `msdcheckset`, `mstinquirysource` | `amc` (header), `contractdetails` (one per serial), `billingcycle` (bills), `sal_order` + `sal_order_det` + `sal_tax` / `sal_adjust` + `labelrelation` (bill invoices), `contractcall` `type='PMS'` (PM visits); masters `amctype` (Comprehensive / Non-Comprehensive / Warranty), `status` (module AMC), Checklist Master `taskchecklistmaster` + `taskchecklistmasterdet` |
| Code | — | form + view `amc/contract/default.asp`, JS `amc/scripts/contract.js`, backend `amc/app/contract.aspx.cs` |
| Exe | — | module **10. AMC Contract** — classes `AmcContract`, `WarrantyContract` and `AmcBillInvoice` (in that order) in `Program.cs` (after modules 1–9) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) — 4,528 contracts; 4,000 billed bills; 4,262 invoices with warranty serials | |
| Status | | migrated and verified |

---

## 2. Field mapping

### 2.1 Contract header → `amc`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `trhcontr.vtrnprefix` + `ntrnno` | `amc.contractno` | Contract Number | Contract No | `MC…` (restarts per office) |
| 2 | Trn. Date | `dtrndate` | `amc.contractdate` | Contract Date | Contract Date | |
| 3 | Party | `nparty` → `mstparty` | `amc.ccode` | Contact Name | Customer | party → contact (module 1) |
| 4 | (Bill To) | the party's default address | `amc.contactbranch` | — | Customer Billing Branch ✚ | place - address; the Contact Branch form field is medispec-only |
| 5 | Party Contact | `npartycontact` → `msdparty` | `amc.contactperson` | Contact Person | Contact Person | `mltcontact.code` |
| 6 | Sales Person | `nsalesman` → `mstusers` | `amc.executive` | Contract Executive Name | Executive | user by name; Admin when blank |
| 7 | AMC Quote No. | `namcquoteno` → migrated AMC quotation (branch + `QA` no.) | `amc.module = 'CQ'`, `modulecode = inqcs.code` | Quotation | Quotation No | 4,165 |
| 8 | Pymt. Schedule + B / E of Period | `npaymentschedule` → `mstpaymentschedule`, `vbegorend` | `amc.paymentterms` | Payment Terms | Payment Terms | `Half Yearly - Beginning of Period` (all 4,528) |
| 9 | (contract type) | serial `ncontrtype` | `amc.amctype` | AMC Type | AMC Type | 1 Comprehensive / 2 Non-Comprehensive (type of most serials) |
| 10 | (period) | serial `dstartdate`, `denddate`, `nmonths` | `amc.startdate`, `enddate`, `contractyears`, `contractmonths` | Start / End Date, Years / Months | same | earliest start – latest end; months ÷ 12 |
| 11 | (renewal) | line `ncontractis` = TRANSFER FROM CONTRACT, `npreviousno` | `amc.oldcontractcode` (previous `renew = 1`) | — | Old Contract | 2,935 renewals; previous contracts `renew = 1` 3,308 |
| 11a | Contract Is (header) | line `ncontractis` → `mstfixedselection` (FRESH / TRANSFER FROM CONTRACT / TRANSFER FROM WARRANTY; one value per contract) | `amc.contracttype` = `miscellaneous.code` (module `AMC Contract`, type `Contract Type`; exe seeds the 3 names) | Contract Type (list column + view) | Contract Type | 4,520 of 4,528 contracts filled (FRESH 361, TRANSFER FROM CONTRACT 3,161, TRANSFER FROM WARRANTY 998), verified in the DB on 23/09/2026. The value also stays in the line remark (row 30). |
| 12 | P.O. No. / P.O. Date | `vpono`, `dpodate` | `amc.ponum`, `podate` | PO Number / PO Date | same | 21 |
| 13 | Campaign | `ntrhcampa` | — | — | — | 0 contracts |
| 14 | Sales Source | `nsalessource` → `mstinquirysource` | part of `amc.remarks` | Remark | Remark | `Sales Source: IN OFFICE (SAKSHAM)` (1) |
| 15 | Terms & Cond. | `nterms` → `msttermset` | part of `amc.remarks` | Remark | Remark | `Terms & Cond.: …` (741); `amc` has no terms field |
| 16 | Check List | `ncheckset` → `mstcheckset` | — | — | — | the set is seeded as a Checklist Master (§3); its name is not written into the remark; AMC contracts have no check items in eBizWiz |
| 17 | Ref. No. / Ref. Date, Remarks, Comment | `vrefno`, `drefdate`, `vremarks`, `vcomment` | `amc.remarks` | Remark | Remark | labelled |
| 18 | Trn. Total, charges, amount received | `ntotalamount`, `trdcontr4posttaxchgs`, `namountrecd` | part of `amc.remarks` | Remark | Remark | `Charges (eBizWiz): …`, `eBizWiz Contract Total: …`, `Amount Received: …` — no tax on the standard contract (the tax is on the bill invoices, 2.4) |
| 19 | Select Letterhead to print | `vletterhead` | — | — | — | print option |
| 20 | (office) | `nofficeid` | `amc.branch` | Branch | Branch | branch 3–6 |
| 21 | — | — | `amc.salcode` | — | — | **never set**: editing a contract deletes the invoice in `salcode`; bills link through `billingcycle.salcode` (2.4) |

### 2.2 Contract Items → `contractdetails` (one per serial)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 22 | Item / Item Code | `trdcontr1items.nitem` | `contractdetails.productcode` | Product | Product | product by Item Code (module 2, `product.casno`) |
| 23 | Serial No. | `trdcontr2itemsdet.vserialno` | `contractdetails.srno` | Serial No | Serial No | each serial row belongs to its own `ncontr` |
| 24 | Rate | `nrate` | `contractdetails.amount` | Price | Amount | net rate; master rate / discount % in the line remark |
| 25 | Location | `vlocation` | `contractdetails.location` | Area | Location | |
| 26 | First installation | `dfirstinstdate` | `contractdetails.installationdate` | Installation Date | Installation Date | 2,271 |
| 27 | (serial period) | `dstartdate`, `denddate` | `contractdetails.startdate`, `enddate` | Start / End | same | |
| 28 | PM visits | `npmvisits` / `nmonths` | `contractdetails.intervals` | Visits | Visits | visits per year when 0/1/2/3/4/6/12 |
| 29 | Contract Is = TRANSFER FROM WARRANTY + previous invoice | `ncontractis`, `npreviousno` → `trhsales` | `contractdetails.invoiceno`, `invoicedate` | Invoice No / Date | same | 1,113 serials |
| 30 | Contract type, months, PM visits, closed, master rate / discount %, contract is, previous contract | `ncontrtype`, `nmonths`, `npmvisits`, `bclosed`, `nmasterrate`, `ndiscountperc`, `ncontractis`, `npreviousno` | `contractdetails.remark` | Product Desc. ✚ | Product Desc. ✚ | labelled lines |
| 31 | Quantity / Main Item / Auto Generated / Technical Set | `nquantity`, `bmainitem`, `bautoserial`, `ntechnicalset` | `contractdetails.quantity` = 1 | Quantity | Quantity | one line per serial; the flags are entry helpers |

### 2.3 Bills → `billingcycle`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 32 | Bill date / amount | `trdcontr3bills.dbilldate`, `nbillamount` | `billingcycle.billingdate`, `amount` | Billing Cycle tab ✚ | Billing Cycle tab ✚ | ordered by date; `type = 'BILL'` |
| 33 | (bill number in the app) | — | `billingcycle.billingno` | Billing Cycle tab ✚ | Billing Cycle tab ✚ | `BILL_` + right 10 of the contract no. + `_n` (app format) |
| 34 | (billed / unbilled) | `trdcontr3bills.nbillno` | `billingcycle.salcode` | Billing Cycle "Billed" | Billing Cycle "Billed" · Invoice tab ✚ | bill with a number → its Sales Invoice (2.4); bill without a number → Unbilled |

### 2.4 Billed bills → Sales Invoice (`AmcBillInvoice`)

The app bills a contract billing cycle with a Sales Invoice linked by `billingcycle.salcode`; the exe does the same for every eBizWiz bill with a bill number (4,000, unique per office; 4 zero-amount bills are skipped).

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 35 | Bill No. / Bill Date | `trdcontr3bills.nbillno`, `dbilldate` | `sal_order.salorderno`, `salorderdt` (`type = 'SO'`) | Invoice No. / Invoice Date | page title / Sales Date | invoice no. = eBizWiz bill no. |
| 36 | (contract party, contact, sales person, Bill To, office) | from the migrated contract | `sal_order.ccode`, `contactperson`, `executive`, `cbranchcode` (= `amc.contactbranch`), `branchcode`, `currency` | Customer, Contact Person, Executive, Customer Billing Branch, Branch Name, Currency | Customer Name, Person Name, Executive Name, Customer Billing Branch, Company Branch, Currency | Admin when the contract has no executive; currency INR |
| 37 | (bill line) | `nbillamount` × contract item total ÷ contract total | `sal_order_det` (product **"Amc Product"**, qty 1, `price` = scaled item value, `prod_desc`) | Product Name, Quantity, Price (Per Unit), Product Description | Particulars, Quantity/Unit, Price/Unit | description `AMC Contract MC… (period), Bill n of m` |
| 38 | (bill GST / charges) | `trdcontr4posttaxchgs` | `sal_order_det.gst` + `sal_tax`, `sal_adjust` | GST (%), Adjustments row | CGST / SGST / IGST columns, Total GST, Adjustments | contract charges scaled to the bill share (PERCENTAGE stays a %, AMOUNT is scaled); GST by the contract total (`GstByContractTotal`, §7) |
| 39 | Bill Amount | `nbillamount` | invoice total (lines + `sal_tax` + `sal_adjust`) | — | Grand Total | = eBizWiz bill amount; any rest → one adjustment `Bill rounding (eBizWiz)` (≤ ₹1) or `Bill difference (eBizWiz)` |
| 40 | (bill reference), Amount Received | `namountrecd` | `sal_order.remarks` | Remarks | Remarks | `AMC Contract: MC… \| Bill n of m (eBizWiz AMC bill) \| eBizWiz Bill Amount: … \| Amount Received: …`; receipts are allocated to these invoices (13) |
| 41 | — | — | `labelrelation` (label category `SAL`) | Labels | Labels | **AMC Contract Bill** |

### 2.5 PM visits → `contractcall` (`type = 'PMS'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 42 | Scheduled date | `trdcontr7pmvisit.dschpmdate` | `contractcall.date`, `complaintdate` | All PMS tab ✚ / AMC Complaint | same | `contractdtcode` = the serial's line |
| 43 | Actual date | `dactpmdate` | `status` Closed + `closedt`; else Open | Status | Status | status module AMC: Open (Pending) / Closed (Completed) |
| 44 | Call | `ncalls` → `trhcalls` | `pmscomplaintno` = `PMS<line>_<n>`, then updated by the call in module 11 | Complaint No | Complaint No | |

### 2.6 Warranty of sold serials → `amc` type Warranty (`WarrantyContract`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 45 | (warranty of an invoice) | `trhsales` with serial rows | `amc` (`amctype` Warranty, `module = 'SAL'`, `modulecode = sal_order.code`) | AMC Contract | invoice view → AMC Contracts | one per invoice; `contractno` = invoice no.; customer, contact, executive, Bill To, branch from the migrated invoice; period = earliest start – latest end |
| 46 | View Serial No. (serial, warranty start / end, months, PM visits, install date, location) | `trdsales2itemsdet` | `contractdetails` (`amount` 0, `invoiceno` / `invoicedate` = the invoice) | lines | lines | one per serial |
| 47 | PM visit schedule | `trdsales7pmvisit` | `contractcall` `PMS` | All PMS tab ✚ | same | Open / Closed as 2.5 |

✚ = opened for saksham (§4a).

---

## 3. Masters seeded

| Master | Our table | Rows added | Source |
|---|---|---|---|
| AMC status | `status` (module AMC) | Open (behavior Pending, default), Closed (behavior Completed) — Cancelled is seeded by module 11 | the names / behaviors the app looks up |
| Contract type | `amctype` | none (1 Comprehensive, 2 Non-Comprehensive, 3 Warranty exist) | — |
| AMC invoice product | `product` | "Amc Product" (service), only when missing | the product the app itself uses for an AMC invoice (`GetSalesAmcProduct`) |
| Sales label | `label` (category `SAL`) + `labelrelation` | "AMC Contract Bill", reused by name | one relation per bill invoice |
| Checklist Master | `taskchecklistmaster` + `taskchecklistmasterdet` (title, days, sort) | 24 sets / 253 items — shared by all modules with a check list, seeded once, idempotent by title | `mstcheckset` + `msdcheckset` (+ `mstchecks` names, `ndaysdiff`): sets of offices 2/3/4/6 that are active or used by a migrated document |

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | AMC Contract view | **Billing Cycle, Invoice, All Complaints, All PMS, Expense** tabs (and their panes) opened for saksham by adding it to the existing company condition | Behaviour | `amc/contract/default.asp` (tab list + pane block) | `billingcycle`, `contractcall`, `sal_order`, `expense` |
| 2 | AMC Contract view + line add | Line **Product Desc.** column shown for saksham — header, cell, colspans, add-line textarea / validation / save | Behaviour | `amc/contract/default.asp`, `amc/scripts/contract.js` | `contractdetails.remark` |
| 3 | AMC Contract view | medispec "Contact Branch" row opened for saksham: Bill To (`amc.contactbranch`, place - address) | Behaviour | `amc/contract/default.asp`, `amc/scripts/contract.js` | `amc.contactbranch` |
| 4 | AMC Contract view | that row labelled **Customer Billing Branch** for saksham (same label as the Sales Invoice form); view only — no form field for saksham | Label | `amc/contract/default.asp` | `amc.contactbranch` |

Originals are kept as comments ("- Aftab Alam"). JS and backend of the tabs have no company check; the Installation tab inside the same block stays gated.

**New fields: 0** · tabs opened: 5 · line column opened: 1 · view rows opened: 1.

### 4b. Database changes (ALTER)

None.

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Campaign | `ntrhcampa` | 0 | empty |
| Check list items | `trdcontr6checks` | 0 | empty — AMC contracts have no check items in eBizWiz; the check sets are in the Checklist Master |
| Technical Set | `trdcontr5tech` | 0 | empty |
| Terms & Cond. text | `vtermsconditions` | 0 | empty |
| Threshold time / manufacturer | `nthresholdtime`, `nmanufacturer` | 0 / 7 | no field |
| Sales Invoice for zero-amount billed bills | `trdcontr3bills` | 4 | nothing to invoice; the billing cycle row exists |
| Received amount on bills | `trdcontr3bills` received / `namountrecd` | — | receipts are module 12 (Payment, 13), allocated to the bill invoices; the amount is also in the invoice remark |
| Upload Doc | `trhdoc` | — | DMS is a separate round |
| Serial lines of 8 contracts | `trdcontr2itemsdet` | 0 | these contracts have no serial in eBizWiz; header only, `amctype` / period empty |

---

## 6. Verification (latest run)

Run ALL, 0 errors:

| Check | Client | Ours |
|---|---|---|
| Contracts | 4,528 | **4,528** `amc` (Comprehensive / Non-Comprehensive); `amc.salcode` set on 0 |
| Quotation link | 4,165 | **4,165** `module = 'CQ'` |
| Contact person | 3,996 set, 34 point to a blank / missing client person | **3,962** |
| Sales person / Payment Terms / PO | 4,023 / 4,528 / 21 | same |
| Sales Source | 1 | `Sales Source:` in **1** contract remark |
| Renewals | 2,932 contracts with a previous contract found | `oldcontractcode` **2,935**; previous contracts `renew = 1` **3,308** |
| Lines | 7,187 serial rows | **7,186** (1 serial row has no product line); first install **2,271**; warranty-invoice lines **1,113** = client |
| Contract amount | Σ serial rates of each contract | equal on **4,528 of 4,528** |
| Bills | 7,618 | **7,618** `billingcycle` = ₹22,60,67,772.51 |
| Bill invoices | 4,000 billed bills (4 of them zero) | **3,996** Sales Invoices linked by `billingcycle.salcode`, total **₹11,70,73,427.85** = client billed bills; `Bill difference (eBizWiz)` on 14 bills of 7 contracts |
| PM visits | 14,219 (11,502 done) | **14,219** PMS (Closed 11,502), 0 orphans |
| Warranty contracts | 4,262 invoices with serials (1 has only an unmigrated item), 6,937 serials, 5,131 PM visits (2,029 done) | **4,261** / **6,936** / **5,131** (Closed 2,029, Cancelled 2,196 after their call, Open 908); customer / number / branch = invoice on all |
| PMS without a call | — | **2,176** (no call in eBizWiz either; see 12) |

---

## 7. Notes

- **`amc.salcode` stays empty.** `deleteSalesEntry` in `contract.aspx.cs` deletes the invoice held in `amc.salcode` whenever the contract or a line is edited. Bills link to their invoices through `billingcycle.salcode` (read by the Billing Cycle tab, the contract Invoice tab, the contract outstanding and the complaint view); the warranty link uses `module = 'SAL'` / `modulecode`, which the invoice view reads.
- **Bill invoices follow the app's own AMC billing**: a Sales Invoice with the service product "Amc Product" (qty 1), `sal_order.module` empty, linked by `billingcycle.salcode`. Receipts (module 12) are allocated to these invoices instead of sitting on account.
- **Bill value.** Item value = the contract header item total (the serial sum only when the header has none) × bill amount ÷ contract total; the contract's charges are scaled the same way.
- **GST by the contract total (`GstByContractTotal`).** A GST row whose amount is not in the eBizWiz contract total (total = items + other charges; 1 contract, MC360) is dropped; a contract with no GST row whose total = (items + other charges) × (1 + r%) for r = 18 / 12 / 5 / 28 gets that GST added (76 contracts, all 18%); everything else is taken as entered. What the invoice still cannot carry ends in `Bill difference (eBizWiz)` — 14 bills of 7 contracts whose own figures disagree.
- **Check lists.** The check-set name is not written into any remark; the eBizWiz check sets are the standard Checklist Master. AMC contracts have no check items in eBizWiz.
- **Serial → contract by the serial's own `ncontr`.** 20 client serial rows point to a product line of another contract; the header totals follow the serial's own contract.
- **Renewals.** 296 client contracts renew several old contracts: every previous contract gets `renew = 1`, `oldcontractcode` = the first, each line remark names its own previous contract. 10 self-referencing renewals are ignored.
- **`billingno` repeats across offices** (4,786 distinct of 7,618) because MC numbers restart per office and the app format uses only the contract number.
- **Warranty = AMC contract type Warranty** (the EdifyBiz standard; no separate warranty module). The app's own "Convert To AMC Contract" creates lines but no PMS rows, so the exe writes the PM visits the way it writes AMC ones. Future PMS rows have no engineer in the client (`assignedto` empty); the calendar lists a PMS once it is assigned.
- **Tabs.** The five tabs are otherwise behind a Zinq / Skytech / enliten / medispec / demo condition; saksham has them to see a contract's bills, invoices, PM visits and calls. The Product Desc. column is otherwise Zinq / Skytech only.
