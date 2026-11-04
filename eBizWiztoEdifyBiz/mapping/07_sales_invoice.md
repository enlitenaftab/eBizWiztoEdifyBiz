# 07 — Sales Invoice

| | |
|---|---|
| **Client screen** | eBizWiz → `Sales > Warranty Sales` (Product Sale Entry) and `Sales > Non Warranty Sales` (Spare Part Sale Entry), and from **More**: Items, Post Tax Charges, Payment Terms, Terms & Cond., Check List |
| **Our screen** | EdifyBiz → `GST Sales > Sales Invoice` |
| **Source tables** | `trhsales` (header), `trdsales1items` (items), `trdsales2itemsdet` (serial / warranty), `trdsales3payterms` (payment terms), `trdsales4posttaxchgs` (post tax charges + GST), `trdsales6checks` (check list), `trdsales5tech` (technical details) |
| **Target tables** | `sal_order` (`type='SO'`), `sal_order_det`, `sal_tax`, `sal_adjust`, `labelrelation`, `taskchecklist` (`module='SAL'`) |
| **Code** | `Program.cs` → `static class SalesInvoice` |
| **Runs** | After Sales Order and Customer Quotation, so the invoice can link back to the order or quotation it came from |

Both client menu items are **one table**, `trhsales`, told apart by `bwarranty`: 4,313 Warranty Sales and 1,394 Non Warranty Sales (4 offices). Both share the `SA` number series. In EdifyBiz they become one Sales Invoice, and the menu they came from is kept as a label.

---

## 1. Migrated

### 1.1 Sale Entry header → `sal_order`  (main form)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Trn. No. | `trhsales.vtrnprefix` + `ntrnno` | Invoice No. | `sal_order.salorderno` | e.g. `SA2496` |
| 2 | Trn. Date | `trhsales.dtrndate` | Invoice Date | `sal_order.salorderdt` | |
| 3 | Party | `trhsales.nparty` → `contact` | Customer Name | `sal_order.ccode` | Resolved through the migrated Contact |
| 4 | Party Contact | `trhsales.npartycontact` → `mltcontact` | Contact Person | `sal_order.contactperson` | |
| 5 | Sales Person | `trhsales.nsalesman` → `users` | Executive | `sal_order.executive` | Falls back to the migration user |
| 6 | Sales Type | `trhsales.nsalestype` → `mstfixedselection` | Order Type | `sal_order.ordertype` | `FRESH` / `AGAINST ORDER RECEIVED`, capped 50 |
| 7 | Bill To | `trhsales.nbillto` → `mltaddress` | Customer Billing Branch | `sal_order.cbranchcode` | Blank Bill To in eBizWiz means the party itself, so the party's own branch is used — the customer GST state depends on it |
| 8 | Ship To | `trhsales.nshipto` → `mltaddress` | Customer Shipping Branch | `sal_order.shippingbranch` | |
| 9 | P.O. No. | `trhsales.vpono` | PO Number | `sal_order.ponumber` | |
| 10 | P.O. Date | `trhsales.dpodate` | PO Date | `sal_order.purorderdt` | |
| 11 | Dispatch Mode | `trhsales.ndispatchmode` → `mstfixedselection` | Dispatch Mode | `sal_order.dispatchmode` | Seeded into `miscellaneous(Sales / Dispatch Mode)` |
| 12 | Dispatch Through | `trhsales.vdespatchthru` | Courier Name | `sal_order.couriername` | |
| 13 | Docket No. | `trhsales.vdespdocno` | Courier No. | `sal_order.courierno` | |
| 14 | Docket Date | `trhsales.ddespdocdate` | Dispatch Date | `sal_order.dispatchdate` | |
| 15 | — (order / quotation link) | `trhsales.nordrc`, else `nquote` | SO / Quotation | `sal_order.inqcsso` | Resolved by the document's own Trn. No. to the migrated Sales Order, else the migrated Quotation |
| 16 | Remarks | `trhsales.vremarks` | Remarks | `sal_order.remarks` | Other screen fields are appended here, see section 2 |
| 17 | — | fixed: INR | Currency | `sal_order.currency` | eBizWiz keeps no currency on this screen |
| 18 | Warranty / Non Warranty (the menu used) | `trhsales.bwarranty` | Label | `labelrelation` → `label` category `SAL` | `Warranty Sales` or `Non Warranty Sales`, as the app's own Label action saves it |
| 19 | (record office) | `trhsales.nofficeid` | Branch | `sal_order.branchcode` | Office → `companyaddress` branch |
| 20 | (audit) | `addedon` / `addedby` | Created On / By | `sal_order.createdon` / `createdby` | |
| 21 | (audit) | `editedon` / `editedby` | Updated On / By | `sal_order.updatedon` / `updatedby` | Falls back to added on / by |

### 1.2 Sales Items → `sal_order_det`  (Items screen, from More)

Both grids — Warranty "Sales Items" and Non Warranty "Non Warranty Sales Items" — are the same table.

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Item / Item Code | `trdsales1items.nitem` → `product` | Product | `sal_order_det.prodcode` | A line whose item does not resolve is skipped and counted |
| 2 | Quantity | `trdsales1items.nquantity` | Quantity | `sal_order_det.qty` | |
| 3 | Master Rate | `trdsales1items.nmasterrate` | Price | `sal_order_det.price` | Used when the line's Discount % reproduces the net rate within 0.01 |
| 4 | WS Rate / Warr. Sale Rate / Non-Warr. Sale Rate | `trdsales1items.nrate` | (net of discount) | — | The net rate. No usable Discount % → it becomes the Price |
| 5 | Discount % | `trdsales1items.ndiscountperc` | Discount | `sal_order_det.discount` | Written as a **line total**: Quantity × Master Rate × Discount % ÷ 100 |
| 6 | Tax Set | `trdsales1items.ntaxset` → `msttaxset` | — | used for `sal_tax` | Line GST %, used only when the header carries no GST charge (section 1.3) |
| 7 | Add Serial No. | `trdsales2itemsdet.vserialno` | Installed Remarks | `sal_order_det.installedremarks` | Written as `Serial No: …`, capped 1,000 |
| 8 | (serial detail) | `trdsales2itemsdet.nmonths` | Warranty Months | `sal_order_det.warrantymonths` | Defaults to 0 — the column is NOT NULL |
| 9 | (serial detail) | `trdsales2itemsdet.dinstdate` | Installation Date | `sal_order_det.installationdt` | |
| 10 | (serial detail) | `trdsales2itemsdet.vlocation` | Location | `sal_order_det.location` | |
| 11 | (serial detail) | serial + warranty + install, joined | Product Description | `sal_order_det.prod_desc` | One readable line per serial, shown under the product |
| 12 | — | the product's Default Batch | Batch | `sal_order_det.prodbatchcode` | Created by the Product module |

### 1.3 Post Tax Charges → `sal_tax` and `sal_adjust`  (Post Tax Charges screen, from More)

In eBizWiz the GST itself is a post-tax charge, so this one screen produces both the tax rows and the other charges.

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | GST charge (`Add : IGST @18%`, `CGST + SGST`, `GST`) | `trdsales4posttaxchgs` | Tax | `sal_tax` | Rebuilt as real tax rows. Same state → CGST + SGST (half each), different state → IGST, decided by the branch and customer GST state, exactly as the sales view does |
| 2 | Line Tax Set | `trdsales1items.ntaxset` | Tax | `sal_tax` | Used only when the header has no GST charge |
| 3 | Every other charge (freight, packing, `TOTAL …`) | `trdsales4posttaxchgs` → `mstprepostchgs.vname` | Adjustment | `sal_adjust` | Name, percent and amount as entered |
| 4 | A discount entered as an AMOUNT before GST | `trdsales4posttaxchgs` | Discount | `sal_order_det.discount` | Spread onto the lines instead of becoming an adjustment, so the line tax equals the eBizWiz GST |
| 5 | Amount / Percentage | `venteredinamtorper` | — | — | A PERCENTAGE charge is applied on the item total; an AMOUNT charge is taken as entered |

A `GST difference` adjustment is added when the rebuilt tax still does not reach the eBizWiz figure, so the invoice total matches.

### 1.4 Check List → `taskchecklist` (`module='SAL'`)  (Check List screen, from More / Task Check List tab)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Check | `trdsales6checks.ncheck` → `mstchecks.vname` | Title | `taskchecklist.title` | Falls back to `Check <code>`. Source holds only **57 rows** |
| 2 | Done | `bdone` | Status | `taskchecklist.status` | Done → 1, else 0 |
| 3 | Expected Date | `dexpdate` | Due Date | `taskchecklist.duedate` | Empty → done date, else today + 7 days |
| 4 | Alloted To | `nallotedto` → `users` | Assign To | `taskchecklist.assignto` | |
| 5 | Remarks | `vremarks` | Remark | `taskchecklist.remark` | |
| 6 | (grid order) | `ncode` | — | `taskchecklist.sort` | |
| 7 | (audit) | `addedon` / `editedon` | Created / Updated On | `taskchecklist.createdon` / `updatedon` | |

### 1.5 Masters seeded

| Source | Target |
|---|---|
| `mstfixedselection` (`ndispatchmode`) | `miscellaneous(Sales / Dispatch Mode)` |
| — | `label` category `SAL`: `Warranty Sales`, `Non Warranty Sales` |
| — | GST tax codes CGST / SGST / IGST, if missing |

### 1.6 Used by later modules

`trdsales2itemsdet` and `trdsales7pmvisit` are read again by the **Warranty Contract** part of module 10: every serial sold here becomes an AMC contract of type `Warranty`, with its PM visit schedule. That mapping is documented with AMC Contract, not here.

---

## 2. Folded into a text field

| # | Client UI | Client table.column | Lands in | Written as |
|---|---|---|---|---|
| 1 | Comment | `trhsales.vcomment` | `sal_order.remarks` | `Comment: <value>` |
| 2 | Ref. No. | `trhsales.vrefno` | `sal_order.remarks` | `Ref No: <value>` |
| 3 | Ref. Date | `trhsales.drefdate` | `sal_order.remarks` | `Ref Date: dd/MM/yyyy` |
| 4 | Sales Source | `trhsales.nsalessource` → `mstinquirysource` | `sal_order.remarks` | `Sales Source: <name>` |
| 5 | Demo | `trhsales.bdemo` | `sal_order.remarks` | `Demo: Yes` — only when ticked |
| 6 | Trn. Total | `trhsales.ntotalamount` | `sal_order.remarks` | `eBizWiz Invoice Total: <value>` — kept for reference, since EdifyBiz recomputes the total |
| 7 | (amount received) | `trhsales.namountrecd` | `sal_order.remarks` | `Amount Received: <value>` — payments themselves live in the Payment module |
| 8 | Payment Terms (whole screen) | `trdsales3payterms` → `mstpaymentterms.vname` | `sal_order.term` | `Payment Terms: <term> <percent>% = <amount>; …` |
| 9 | Terms & Cond. | `trhsales.nterms` → `msttermset.vname` | `sal_order.term` | `Terms & Cond.: <set name>` |

---

## 3. Skipped

Every column below was searched across the whole of `Program.cs`, not just this module — none is read anywhere.

### 3.1 `trhsales` (header)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `ntrhcampa` | yes — Campaign | Campaigns migrate to the Mass Mail module; an invoice has no campaign field |
| 2 | `ncheckset` | yes — Check List | The check-list **set name** is not stored; its items migrate from `trdsales6checks` (section 1.4) |
| 3 | `nitemtotal`, `ndiscount`, `ntaxset` | no | Header totals and tax set; the real values come from the lines and the post-tax charges |
| 4 | `vletterhead`, `vletterheadlink` | yes — Select Letterhead to print | Print option of the eBizWiz report |
| 5 | `vtermsconditions` | no | Free-text terms; only the Terms & Cond. **set name** migrates (section 2 #9) |
| 6 | `bapproval`, `approvedby`, `approvedon` | no | The client's sale screen has no approval step |
| 7 | `VERPCODE` | no | External ERP key |
| 8 | `ipaddress` | no | Audit only |

### 3.2 `trdsales1items` (Items)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `ndiscount` | yes — Discount Amt. | Never deducted in eBizWiz. The discount is rebuilt from Master Rate and Discount % instead (section 1.2 #5) |
| 2 | `ntechnicalset` | yes — Technical Set | Technical specification sets are not migrated |
| 3 | `bmainitem` | yes — Main Item | Main / sub item grouping has no EdifyBiz equivalent |
| 4 | `bautoserial` | yes — Auto Generat Dummy Serial No. | A data-entry helper, not data |
| 5 | `ntotrecdin`, `ntotacceptin`, `ntotrejectin`, `ntotmissingin` | no | Received / accepted / rejected / missing quantities, no equivalent |
| 6 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Line audit; the invoice's dates are used on every line |

### 3.3 `trdsales2itemsdet` (Serial / warranty detail)

Read here for the serial, warranty months, install date and location (section 1.2). The rest is used by the Warranty Contract module.

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nthresholdtime`, `binstallationcall`, `bwvoid`, `balreadyregister` | no | eBizWiz service flags, no EdifyBiz equivalent |
| 2 | `doemstartdate`, `noemmonths`, `doemenddate` | no | OEM warranty window, no equivalent |
| 3 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only |

### 3.4 `trdsales4posttaxchgs` (Post Tax Charges)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `brunningposttax` | yes — Apply to Running Post Tax Total Only | Verified against `ntotalamount`: applying every percentage charge on the item total matches more documents than a running base |
| 2 | `bapplyonitemtotalonly` | yes — Apply to Item Total Only | Read while calculating, but not stored on the tax or adjustment row |
| 3 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only |

### 3.5 `trdsales6checks` (Check List)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nexpense` | no | `taskchecklist` has no expense column |
| 2 | `npendingreason` | no | `taskchecklist` has no pending-reason column |
| 3 | `addedby`, `editedby`, `ipaddress` | no | Audit only |

### 3.6 `trdsales3payterms` (Payment Terms)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only. The data columns are folded into `sal_order.term` (section 2 #8) |

### 3.7 `trdsales5tech` (Technical details)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | whole table | yes — Technical Set (per item) | Technical records are not migrated. The table is **empty** — 0 rows |
