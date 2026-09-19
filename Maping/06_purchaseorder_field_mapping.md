# 06. Order Placed → Purchase Order

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Order Placed** — header, commission / OR payment / custom clearance block, *Order Placed Items*, **More** menu: Post Tax Charges, Payment Terms, Terms & Cond., Check List, Add Against Order Received Items, Upload Doc | **Purchase Order** (`/edify/gst/purchase/`, type `PO`) — form, view, Product Details, Task Check List tab |
| Tables | `trhordpl` (header), `trdordpl1items` (items), `trdordpl2deldate` (delivery date per line), `trdordpl3payterms` (payment terms), `trdordpl4posttaxchgs` (charges), `trdordpl5checks` (check list); masters `mstfixedselection` (Order Type, Order Placed Type, Order Status), `mstcallpendingreasons`, `mstinquirycategory`, `mstcurrency`, `mstpaymentterms`, `msttermset`, `mstcheckset` + `msdcheckset`, `mstchecks`, `mstprepostchgs` | `pur_order` (`type = 'PO'`), `pur_order_det`, `taskchecklist` (`module = 'PUR'`); masters `status` (Purchase Order), `miscellaneous` (Purchase Order / Pending Reason, Inquiry / Category), `currency`, Checklist Master (`taskchecklistmaster` / `taskchecklistmasterdet`) |
| Code | — | form + view `gst/purchase/default.asp`, JS `gst/purchase/script/purchaseorder.js`, backend `gst/purchase/app/purchaseorder.asp` (save by field name through `SqlOperation`) |
| Exe | — | module **6. Purchase Order** — class `PurchaseOrder` in `Program.cs` (after module 5, so the Sales Orders exist) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) — 6,031 orders | |
| Status | | migrated and verified |

---

## 2. Field mapping

### 2.1 Header → `pur_order` (`type = 'PO'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `trhordpl.vtrnprefix` + `ntrnno` | `pur_order.purorderno` | Purchase Order Number | page title | e.g. `OP2936` |
| 2 | Trn. Date | `dtrndate` | `pur_order.purorderdt` | Purchase Order Date | Purchase Order Date | |
| 3 | Order Type | `nordplcdtype` → `mstfixedselection` | — | — | — | ON PARTY on all 6,031 |
| 4 | Order To | `ntoparty` → `mstparty` | `pur_order.scode` | Supplier Name | Supplier Name | party → contact (module 1) |
| 5 | Order Placed Type | `nordertype` → `mstfixedselection` (FRESH 372 / AGAINST ORDER RECEIVED 5,659) | — | — | — | AGAINST = the Sales Order link (row 26) |
| 6 | Pending Reason | `npendingreason` → `mstcallpendingreasons` ("OPD - …") | `pur_order.pendingreason` | **Pending Reason** ✚ | **Pending Reason** ✚ | `miscellaneous` Purchase Order / Pending Reason by name, seeded if missing |
| 7 | Show Items that have reached MOL or ROL / Show Parts Required of Service Calls | — | — | — | — | item search filters, nothing stored |
| 8 | Terms & Cond. | `nterms` → `msttermset` (ESCO Standard Terms and Conditions, 405) | part of `pur_order.term` | Terms | Terms / Term Details | `Terms & Cond.: <set name>` |
| 9 | (terms text of that set) | `vtermsconditions` (HTML, 420) | `pur_order.pay_notes` | Pay Notes | Pay Note | the CKEditor field, so the HTML shows formatted |
| 10 | Check List | `ncheckset` → `mstcheckset` | — (the set is a Checklist Master, §3) | — | — | the set name is not written into any remark; the PO's check items are in 2.4 |
| 11 | Order Currency | `ncurrency` → `mstcurrency` | `pur_order.currency` | Currency | Currency | by short name / name |
| 12 | Inquiry Category | `ninquirycategory` → `mstinquirycategory` | `pur_order.inquirycategory` | **Inquiry Category** ✚ | **Inquiry Category** ✚ | `miscellaneous` Inquiry / Category |
| 13 | Bill To | `nbillto` → `mstparty` → its contact's address | `pur_order.cbranchcode` | **Customer Billing Branch** ✎ | **Customer Billing Branch** ✚ (saksham block, branch label) and the standard branch row **Customer Billing Branch** ✎ (branch place) | free-hand branch picker |
| 14 | Ship To | `nshipto` → `mstparty` → its contact's address | `pur_order.shippingbranch` | **Customer Shipping Branch** ✚ | **Customer Shipping Branch** ✚ | free-hand branch picker |
| 15 | Invoice No. | `vinvno` | `pur_order.invoiceno` | **Invoice No.** ✚ | **Invoice No** ✚ | cap 100 |
| 16 | Invoice Date | `dinvdate` | `pur_order.invoicedate` | **Invoice Date** ✚ | **Invoice Date** ✚ | |
| 17 | O/A No | `vrefno` | `pur_order.oano` | **O/A No.** ✚ | **O/A No** ✚ | cap 100 |
| 18 | O/A Date | `drefdate` | `pur_order.oadate` | **O/A Date** ✚ | **O/A Date** ✚ | |
| 19 | Remarks | `vremarks` | `pur_order.remarks` (first part) | **Remarks** ✚ | **Remarks** ✚ | |
| 20 | Comment | `vcomment` | part of `pur_order.remarks` | Remarks | Remarks | `Comment: …` |
| 21 | Dispatch Date | `dprindispatchdate` | `pur_order.deliverydate` | Delivery Date | Delivery Date | 250 orders |
| 22 | Installation Date | `dprininstdate` | part of `pur_order.remarks` | Remarks | Remarks | `Installation Date: dd/MM/yyyy` (33) |
| 23 | Total Discount % | `nprindiscountperc` | `pur_order.discount` | **Discount** ✚ | Discount | 268 orders; shown only, not deducted by our PO total |
| 24 | OR Value Payment Received % / Amt, OR Value Payment Pending % / Amt, Payment Pending Since Date | `nprinorpayrecdperc`, `nprinorpayrecdamt`, `nprinorpaypenperc`, `nprinorpaypenamc`, `dprinpaypenddate` | part of `pur_order.remarks` | Remarks | Remarks | each with its screen label, non-zero only (381 / 189 / 62 orders) |
| 24a | Commission Requested + Commission Requested Currency, Commission Received + Commission Received Currency | `nprincommreq` + `nprinreqcurrency`, `nprincommrec` + `nprinreccurrency` → `mstcurrency` | part of `pur_order.remarks` | Remarks | Remarks | `Commission Requested: 1500 USD` (751 / 239 orders; 4 requested amounts have no currency). Masked on the client screen; migrated on the user's decision (§7) |
| 25 | Total INR Value of Custom Clearance, Custom Clearance Recd. / Pending Amt., CCS Remarks | `nprininrcustcleaamt`, `ncustomclerecamt`, `ncustomclependamt`, `vccsremarks` | part of `pur_order.remarks` | Remarks | Remarks | with screen labels (1 / 7 orders) |
| 26 | (Against Order Received) | line `nordrcditemdetailncode` → Sales Order line → its order | `pur_order.salesordercode` | SO Number | Sales Order No | comma list of migrated SO `inqcs.code`, read from all lines incl. blank-item lines |
| 27 | Order Status | `nordplcdstatus` → `mstfixedselection` | `pur_order.status` | **Status** ✚ | Status | `status` (module Purchase Order) by name, seeded if missing |
| 28 | Trn. Total | `ntotalamount` | — | — | Total Amount | computed from the lines (§7) |
| 29 | Select Letterhead to print | `vletterhead` | — | — | — | print option |
| 30 | (office of the record) | `nofficeid` | `pur_order.branchcode`, `comcode` | Branch Name / Company Name | Branch / Company Name | office → `companyaddress.code`; company 1 |
| 31 | — | — | `pur_order.executive` | Executive | Executive Name | Admin (the client PO has no sales person) |
| 32 | — | `addedon` / `editedon` | `createdon` / `updatedon` | — | — | `createdby` / `updatedby` = migration user |

### 2.2 Order Placed Items → `pur_order_det`

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 33 | Item Name / Item Code | `trdordpl1items.nitem` → `mstitems` | `pur_order_det.prodcode` | Product Name | Particulars | product by Item Code (module 2, `product.casno`) |
| 34 | Quantity | `nquantity` | `pur_order_det.qty` | Quantity | Quantity/Unit | |
| 35 | Master Rate / Discount % / Discount Amt. / Order Rate | `nmasterrate`, `ndiscountperc`, `ndiscount`, `nrate` | `pur_order_det.price` = `nrate` | Price (Per Unit) | Price/Unit | `nrate` is already net of the discount (1,178 of 1,179), so no discount is stored again |
| 36 | Item Currency Is / Currency Conversion Factor | `nitemcurrency`, `ncurrencyvalue` | `pur_order_det.currency` = the PO currency | — | — | the rates are in the PO currency (§7); line currency is not shown on our PO |
| 37 | Remarks | `vdiscountremarks` | `pur_order_det.prod_desc` | Product Description | Particulars (below the name) | `Remarks: …` (23 lines) |
| 38 | Tax Set | `ntaxset` | — | — | — | 7 lines, no tax amount on a PO (§7) |
| 39 | Recd. Qty. | `nquantitytotrecd` | — | — | GRN Qty | computed by our PO from the GRNs of module 8 |
| 40 | Item Total | — | — | — | Amount / Taxable Amount | computed |
| 41 | — | — | `pur_order_det.prodbatchcode` | — | — | the product's Default Batch (module 2) |

### 2.3 Payment Terms + Post Tax Charges

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 42 | Payment Terms, Percentage, Amount | `trdordpl3payterms.nmstpayterms` → `mstpaymentterms`, `npercentage`, `namount` | part of `pur_order.term` | Terms | Terms / Term Details | `Payment Terms: Advance TT 30% = 1500.00; …` before Terms & Cond. (1,915 rows / 1,730 orders) |
| 43 | Post Tax Charges (name, amount / percentage) | `trdordpl4posttaxchgs` → `mstprepostchgs.vname` | part of `pur_order.remarks` | Remarks | Remarks | `Post Tax Charges: Add : Sea Freight & Insurance Charges = 1250.00; …` (2,180 rows / 1,381 orders) |

### 2.4 Check List → `taskchecklist` (`module = 'PUR'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 44 | Check | `trdordpl5checks.ncheck` → `mstchecks` | `taskchecklist.title` | Task Check List tab | Task Check List tab | `modulecode` = `pur_order.code`; tag `PUR` = what `purchaseorder.js` passes |
| 45 | Done / Done Date | `bdone`, `ddonedate` | `taskchecklist.status`, `updatedon` | | | |
| 46 | Expected Date | `dexpdate` | `taskchecklist.duedate` | | | blank → done/created date (done) or today + 7 |
| 47 | Alloted To | `nallotedto` → `mstusers` | `taskchecklist.assignto` | | | user by name |
| 48 | Remarks | `vremarks` | `taskchecklist.remark` | | | |

✚ = field added to our form / view for saksham · ✎ = standard field relabelled for saksham (§4a).

---

## 3. Masters seeded

| Master | Our table | Rows added | Source |
|---|---|---|---|
| Order Status | `status` (module Purchase Order) | 12 (13 values in use) | `mstfixedselection` |
| Pending Reason | `miscellaneous` (Purchase Order / Pending Reason) | 47 | `mstcallpendingreasons` values used |
| Inquiry Category | `miscellaneous` (Inquiry / Category) | 0 (795 mapped, seeded by module 3) | `mstinquirycategory` |
| Check List sets | Checklist Master: `taskchecklistmaster` (title) / `taskchecklistmasterdet` (title, days, sort) | 24 sets / 253 items, shared by all modules (seeded once, by title) | `mstcheckset` + `msdcheckset` + `mstchecks` — sets of offices 2/3/4/6 that are active or used by a migrated document |

Payment Terms, Terms set and charge names are written as text, so no master is seeded for them.

---

## 4. Changes made on our side

### 4a. Form / view changes (saksham-gated)

| # | Screen | Change | Kind | File | DB column |
|---|---|---|---|---|---|
| 1 | PO form | **Status** shown for saksham (added to the existing company gate) | New field | `gst/purchase/default.asp` | `pur_order.status` |
| 2 | PO form | **Discount** shown for saksham (added to the existing gate) | New field | `gst/purchase/default.asp` | `pur_order.discount` |
| 3 | PO form | Supplier Branch is the free-hand Bill To picker, labelled **Customer Billing Branch** | Label + Behaviour | `gst/purchase/default.asp` | `pur_order.cbranchcode` |
| 4 | PO form | **Customer Shipping Branch** (Ship To) free-hand picker | New field | `gst/purchase/default.asp` | `pur_order.shippingbranch` |
| 5 | PO form | **Inquiry Category** + quick-add | New field | `gst/purchase/default.asp` | `pur_order.inquirycategory` |
| 6 | PO form | **Pending Reason** + quick-add | New field | `gst/purchase/default.asp` | `pur_order.pendingreason` |
| 7 | PO form | **Invoice No.**, **Invoice Date** | New field ×2 | `gst/purchase/default.asp` | `invoiceno`, `invoicedate` |
| 8 | PO form | **O/A No.**, **O/A Date** | New field ×2 | `gst/purchase/default.asp` | `oano`, `oadate` |
| 9 | PO form | **Remarks** | New field | `gst/purchase/default.asp` | `pur_order.remarks` |
| 10 | PO view | Saksham block: Customer Billing Branch, Customer Shipping Branch, Inquiry Category, Pending Reason, Invoice No / Date, O/A No / Date, Remarks | New field ×9 | `gst/purchase/default.asp`, `script/purchaseorder.js` | as above |
| 11 | Backend | 13 saksham trailing values in the PO array (codes + names of the fields above) | Backend | `gst/purchase/app/purchaseorder.asp` | — |
| 12 | PO form | Opening the form (add + edit) sets the branch label to "Supplier Branch *" for every company; saksham keeps **Customer Billing Branch** | Label | `gst/purchase/script/purchaseorder.js` | — |
| 13 | PO view | Standard branch row (`#lbl_view_supp_branch`, shows the Bill To branch place) labelled **Customer Billing Branch** instead of Supplier Branch | Label | `gst/purchase/script/purchaseorder.js` | `pur_order.cbranchcode` |

**New fields on the form: 10** (Status, Discount, Customer Shipping Branch, Inquiry Category, Pending Reason, Invoice No., Invoice Date, O/A No., O/A Date, Remarks) · **on the view: 9** · labels: 3 · backend: 1. The Task Check List tab is on the PO view for every company.

### 4b. Database changes (ALTER)

From `00_ALTERS_run_before_exe.sql`:

```sql
IF COL_LENGTH('pur_order','inquirycategory') IS NULL ALTER TABLE pur_order ADD inquirycategory int            NULL;
IF COL_LENGTH('pur_order','pendingreason')   IS NULL ALTER TABLE pur_order ADD pendingreason   int            NULL;
IF COL_LENGTH('pur_order','invoiceno')       IS NULL ALTER TABLE pur_order ADD invoiceno       nvarchar(100)  NULL;
IF COL_LENGTH('pur_order','invoicedate')     IS NULL ALTER TABLE pur_order ADD invoicedate     datetime       NULL;
IF COL_LENGTH('pur_order','oano')            IS NULL ALTER TABLE pur_order ADD oano            nvarchar(100)  NULL;
IF COL_LENGTH('pur_order','oadate')          IS NULL ALTER TABLE pur_order ADD oadate          datetime       NULL;
```

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Order Type / Order Placed Type | `nordplcdtype`, `nordertype` | all | ON PARTY everywhere; AGAINST ORDER RECEIVED = the Sales Order link |
| Payment Pending Days | `nprincpendingdays` | 70 | computed on the client screen |
| Check items of deleted POs | `trdordpl5checks` whose `nordpl` has no `trhordpl` row in the same office | 1,124 items / 58 PO numbers | their PO no longer exists in the client; the same number in another office is a different PO |
| Delivery date per line | `trdordpl2deldate` | 31,732 rows / 5,897 orders | equals the PO date on 5,832 orders — the screen's default, not an entered date; no line date on our PO |
| Recd. / accepted / rejected / missing qty | `nquantitytotrecd`, `nquantitytotaccept`, `nquantitytotreject`, `nquantitytotmissing` | 25,569 lines | our PO computes GRN Qty from the GRNs (module 8) |
| Tax Set on lines | `ntaxset`, `ntaxamt` | 7 lines | no tax on a saksham PO (§7) |
| Select Letterhead to print | `vletterhead` | 2,732 | print option |
| MOL / ROL, pending part flags | `bordmslandrol`, `bpendingpart` | 1 / 0 | item search filters |
| Upload Doc | `trhdoc` | — | DMS is out of scope (user's decision) |

---

## 6. Verification (latest run)

Run ALL, 0 errors.

| Check | Client | Ours |
|---|---|---|
| Orders | 6,031 | **6,031** `pur_order` PO (6,032 incl. the test PO-000123) |
| Supplier | 6,030 with a party | **6,030** resolved, 0 unresolved (OP1856 has no party in eBizWiz either) |
| Lines | 31,364 (31,362 with an item) | **31,362** `pur_order_det`, 0 skipped; all with the product's Default Batch |
| Line currency | 17,215 lines whose item currency ≠ PO currency | **0** migrated lines differ (the only one is the pre-existing test PO-000123) |
| Line remarks | 23 | **23** `prod_desc` |
| Sales Order link | 5,618 POs with a line pointing to a Sales Order line (5,611 AGAINST ORDER RECEIVED + 7 FRESH; 48 AGAINST have no line link in the client) | **5,615** `salesordercode`, 0 wrong (3 links not resolved) |
| Bill To / Ship To | 4,952 / 4,934 | **4,951** / **4,933** correct party (1 each: party no longer exists) |
| Status | 13 values + 389 blank | seeded 12, mapped by name |
| Payment Terms / Terms & Cond. | 1,730 / 405 orders, 2,128 with either | `pur_order.term`: **1,730** / **405**, **2,128** in total |
| Post Tax Charges | 2,180 rows / 1,381 orders | **1,381** remarks with `Post Tax Charges:` |
| Screen fields in remarks | 381 OR received / 189 OR pending / 62 pending since / 33 installation | **381** / **189** / **62** / **33** |
| Commission in remarks | 751 requested / 239 received | **751** / **239** |
| Check list | 83,938 items loaded; 82,814 belong to an existing PO (46,070 done) — 1,124 items on 58 PO numbers that have no header in their office (deleted in the client) | **82,814** `taskchecklist` PUR, **46,070** done |
| Check List sets | sets of offices 2/3/4/6, active or used | Checklist Master **24** sets / **253** items; `Check List:` in PO remarks **0** |

---

## 7. Notes

- **Bill To / Ship To labels.** Form and view use the Sales Invoice form labels **Customer Billing Branch** (Bill To, `cbranchcode`) and **Customer Shipping Branch** (Ship To, `shippingbranch`). On the view both the saksham block rows and the standard branch row (`#lbl_view_supp_branch`) read Customer Billing / Shipping Branch. The "Pay Note" label keeps its standard name; for saksham it holds the Terms & Cond. text.
- **Line currency.** On POs whose items are in another currency, the lines still add up to the header's item total in the PO currency (3,254 of 3,288 such POs), so the rates are PO-currency values and the line carries the PO currency. Our PO view does not print the line currency and has no currency conversion, so this only affects data correctness.
- **Totals.** The saksham PO view (type PO) is outside the tax/adjustment gate in `purchaseorder.js`: it shows no tax columns, no Adjustments (`pur_adjust`) and one "Total Amount" = Σ(qty × price − line discount); the header discount and line freight are not added. The client's charges (fumigation, sea/air freight, courier …) therefore go to Remarks as text, and `ntotalamount` stays the client's reference.
- **Payment Terms** can't go to the Payment Schedule tab (`payment_revenue`): that tab needs a date and has no term / % column, while the client terms have no dates. They go to the "Terms" field instead, same as the quotation and the sales order.
- **Terms & Cond.** `nterms` = 1 on 405 orders is the term set "ESCO Standard Terms and Conditions" (`msttermset`, same master as the quotation); the PRE/POST flag only shares the code value. Its HTML text (`vtermsconditions`) goes to Pay Notes, the only CKEditor field on the PO.
- **Pending Reason** master is `mstcallpendingreasons` ("OPD - Process Completed", "OPD - Shipment Pending" …); joining `mstfixedselection` on the same code gives unrelated values (YES, OPEN, CONTRACT RENEWAL).
- **Commission.** Commission Requested / Received are masked on the client screen (`XXXXXXXX`), while our Remarks are visible to every user who can open the PO. They are migrated into Remarks on the user's decision, so the amounts are not lost.
- **Check lists.** The PO's check items are in the Task Check List tab (`taskchecklist`); the eBizWiz check sets with their items are the standard Checklist Master (§3). The set name is not written into the remark.
- **Approval.** `pur_order.approvalstatus` stays NULL, which is what a PO saved from our form gets (the PO form posts no approval field); the view shows it as "-".
- **Payment Schedule total** on the PO view (`#Total_amt` = subtotal + totalGST) reads a GST value that is only set inside the tax branch, which saksham skips — the header total there is likely `NaN`. Not a migration issue; noted for the form team.
