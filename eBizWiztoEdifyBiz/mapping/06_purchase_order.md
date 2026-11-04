# 06 — Purchase Order

| | |
|---|---|
| **Client screen** | eBizWiz → `Order > Order Placed`, and from **More**: Items, Post Tax Charges, Payment Terms, Terms & Cond., Check List, Add Against Order Received Items |
| **Our screen** | EdifyBiz → `GST Purchase > Purchase Order` |
| **Source tables** | `trhordpl` (header), `trdordpl1items` (items), `trdordpl3payterms` (payment terms), `trdordpl4posttaxchgs` (post tax charges), `trdordpl5checks` (check list), `trdordpl2deldate` (delivery dates) |
| **Target tables** | `pur_order` (`type='PO'`), `pur_order_det`, `taskchecklist` (`module='PUR'`) |
| **Code** | `Program.cs` → `static class PurchaseOrder` |
| **Runs** | After Sales Order, so a PO line can link back to the Sales Order it was raised against |

This is the one module where the client screen is much wider than ours: eBizWiz keeps commission, payment-follow-up and custom-clearance figures on the order header, and EdifyBiz has no columns for them. They are all kept, with their screen labels, in the Remarks (section 2).

---

## 1. Migrated

### 1.1 Order Placed header → `pur_order`  (main form)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Trn. No. | `trhordpl.vtrnprefix` + `ntrnno` | PO Number | `pur_order.purorderno` | e.g. `OP2947` |
| 2 | Trn. Date | `trhordpl.dtrndate` | PO Date | `pur_order.purorderdt` | |
| 3 | Order To | `trhordpl.ntoparty` → `contact` | Supplier Name | `pur_order.scode` | Resolved through the migrated Contact |
| 4 | Order Currency | `trhordpl.ncurrency` → `mstcurrency` | Currency | `pur_order.currency` | |
| 5 | Total Discount % | `trhordpl.nprindiscountperc` | Discount | `pur_order.discount` | |
| 6 | Delivery Date (per line) | `trdordpl2deldate.ddeldate` | Delivery Date | `pur_order.deliverydate` | Earliest date per order. All lines of a PO carry the same date (5,896 of 5,897). Falls back to the header Dispatch Date when a PO has no line date |
| 7 | Terms & Cond. (free text) | `trhordpl.vtermsconditions` | Payment Notes | `pur_order.pay_notes` | |
| 8 | Remarks | `trhordpl.vremarks` | Remarks | `pur_order.remarks` | Many other screen fields are appended here, see section 2 |
| 9 | Order Status | `trhordpl.nordplcdstatus` → `mstfixedselection` | Status | `pur_order.status` | → `status` master |
| 10 | Inquiry Category | `trhordpl.ninquirycategory` → `mstinquirycategory` | Inquiry Category | `pur_order.inquirycategory` | → `miscellaneous(Inquiry / Category)` |
| 11 | Pending Reason | `trhordpl.npendingreason` → `mstcallpendingreasons` | Pending Reason | `pur_order.pendingreason` | → `miscellaneous(Purchase Order / Pending Reason)` |
| 12 | Bill To | `trhordpl.nbillto` → `mltaddress` | Supplier Branch | `pur_order.cbranchcode` | Party → migrated contact → its default branch |
| 13 | Ship To | `trhordpl.nshipto` → `mltaddress` | Shipping Branch | `pur_order.shippingbranch` | |
| 14 | Invoice No. | `trhordpl.vinvno` | Invoice No. | `pur_order.invoiceno` | Capped 100 |
| 15 | Invoice Date | `trhordpl.dinvdate` | Invoice Date | `pur_order.invoicedate` | |
| 16 | O/A No | `trhordpl.vrefno` | O/A No | `pur_order.oano` | Capped 100 |
| 17 | O/A Date | `trhordpl.drefdate` | O/A Date | `pur_order.oadate` | |
| 18 | — (order link) | `trdordpl1items.nordrcditemdetailncode` → migrated Sales Order | SO Number | `pur_order.salesordercode` | Resolved line → Sales Order line → its order. Comma-separated when the PO covers more than one SO |
| 19 | (record office) | `trhordpl.nofficeid` | Branch | `pur_order.branchcode` | Office → `companyaddress` branch |
| 20 | (audit) | `addedon` / `addedby` | Created On / By | `pur_order.createdon` / `createdby` | |
| 21 | (audit) | `editedon` / `editedby` | Updated On / By | `pur_order.updatedon` / `updatedby` | Falls back to added on / by |

`pur_order.type` is `PO` and `pur_order.executive` is the migration user — eBizWiz keeps no salesperson on this screen.

### 1.2 Order Placed Item → `pur_order_det`  (Items screen, from More)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Item Name / Item Code | `trdordpl1items.nitem` → `product` | Product | `pur_order_det.prodcode` | A line whose item does not resolve is skipped and counted, but its Sales Order link is still read |
| 2 | Quantity | `trdordpl1items.nquantity` | Quantity | `pur_order_det.qty` | |
| 3 | Master Rate | `trdordpl1items.nmasterrate` | Price | `pur_order_det.price` | Used as the price when the line carries a discount |
| 3a | Order Rate | `trdordpl1items.nrate` | (net of discount) | — | Already net of the discount in eBizWiz. No discount → it becomes the Price |
| 3b | Discount % | `trdordpl1items.ndiscountperc` | Discount | `pur_order_det.discount` | Written as a **line total**: (Master Rate − Order Rate) × Quantity, because the PO total is `sum((qty × price) − discount)`. 1,178 of 31,364 lines carry a discount % |
| 4 | Remarks | `trdordpl1items.vdiscountremarks` | Product Description | `pur_order_det.prod_desc` | |
| 5 | Item Currency Is | `trdordpl1items.nitemcurrency` | Currency | `pur_order_det.currency` | The **order** currency is used; the item currency only when the order has none |
| 6 | — | the product's Default Batch | Batch | `pur_order_det.prodbatchcode` | Created by the Product module; EdifyBiz lines need a batch |

### 1.3 Check List → `taskchecklist` (`module='PUR'`)  (Check List screen, from More / Task Check List tab)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Check | `trdordpl5checks.ncheck` → `mstchecks.vname` | Title | `taskchecklist.title` | Falls back to `Check <code>` |
| 2 | Done | `bdone` | Status | `taskchecklist.status` | Done → 1, else 0 |
| 3 | Expected Date | `dexpdate` | Due Date | `taskchecklist.duedate` | Empty → done date, else a future date |
| 4 | Alloted To | `nallotedto` → `users` | Assign To | `taskchecklist.assignto` | |
| 5 | Remarks | `vremarks` | Remark | `taskchecklist.remark` | |
| 6 | (grid order) | `ncode` | — | `taskchecklist.sort` | |
| 7 | (audit) | `addedon` / `editedon` | Created / Updated On | `taskchecklist.createdon` / `updatedon` | |

### 1.4 Masters seeded

| Source | Target |
|---|---|
| `mstfixedselection` (`nordplcdstatus`) | `status` |
| `mstinquirycategory` | `miscellaneous(Inquiry / Category)` |
| `mstcallpendingreasons` | `miscellaneous(Purchase Order / Pending Reason)` — shared with Sales Order |
| `mstcurrency` | `currency` |

---

## 2. Folded into a text field

`pur_order` has no column for any of these, so each is written with its client screen label. All go to `pur_order.remarks`, separated by ` | `, except the last two rows.

| # | Client UI | Client table.column | Lands in | Written as |
|---|---|---|---|---|
| 1 | Comment | `trhordpl.vcomment` | `pur_order.remarks` | `Comment: <value>` |
| 2 | Commission Requested + Currency | `nprincommreq`, `nprinreqcurrency` | `pur_order.remarks` | `Commission Requested: 1500 USD`. Masked as `XXXXXXXX` on the client screen; migrated on the user's decision (17/09/2026) |
| 3 | Commission Received + Currency | `nprincommrec`, `nprinreccurrency` | `pur_order.remarks` | `Commission Received: <amount> <currency>` |
| 4 | Dispatch Date | `dprindispatchdate` | `pur_order.remarks` | `Dispatch Date: dd/MM/yyyy`. A different field from the line Delivery Date — filled on only 250 of 6,031 POs, and never equal to the line date |
| 5 | Installation Date | `dprininstdate` | `pur_order.remarks` | `Installation Date: dd/MM/yyyy` |
| 6 | OR Value Payment Received % | `nprinorpayrecdperc` | `pur_order.remarks` | `OR Value Payment Received %: <value>` |
| 7 | OR Value Payment Received Amt | `nprinorpayrecdamt` | `pur_order.remarks` | `OR Value Payment Received Amt: <value>` |
| 8 | OR Value Payment Pending % | `nprinorpaypenperc` | `pur_order.remarks` | `OR Value Payment Pending %: <value>` |
| 9 | OR Value Payment Pending Amt | `nprinorpaypenamc` | `pur_order.remarks` | `OR Value Payment Pending Amt: <value>` |
| 10 | Payment Pending Since Date | `dprinpaypenddate` | `pur_order.remarks` | `Payment Pending Since Date: dd/MM/yyyy` |
| 11 | Total INR Value of Custom Clearance | `nprininrcustcleaamt` | `pur_order.remarks` | `Total INR Value of Custom Clearance: <value>` |
| 12 | Custom Clearance Recd. Amt. | `ncustomclerecamt` | `pur_order.remarks` | `Custom Clearance Recd. Amt.: <value>` |
| 13 | Custom Clearance Pending Amt. | `ncustomclependamt` | `pur_order.remarks` | `Custom Clearance Pending Amt.: <value>` |
| 14 | CCS Remarks | `vccsremarks` | `pur_order.remarks` | `CCS Remarks: <value>` |
| 15 | Post Tax Charges (whole screen) | `trdordpl4posttaxchgs` → `mstprepostchgs.vname` | `pur_order.remarks` | `Post Tax Charges: <name> <amount or percent>; …`. Unlike Sales Order and Quotation, a PO has no adjustment table in EdifyBiz |
| 16 | Payment Terms (whole screen) | `trdordpl3payterms` → `mstpaymentterms.vname` | `pur_order.term` | `Payment Terms: <term> <percent>% = <amount>; …`, one entry per row |
| 17 | Terms & Cond. (set) | `trhordpl.nterms` → `msttermset.vname` | `pur_order.term` | `Terms & Cond.: <set name>` |

A zero or empty value is left out — only filled figures reach the remarks.

---

## 3. Skipped

Every column below was searched across the whole of `Program.cs`, not just this module — none is read anywhere.

### 3.1 `trhordpl` (header)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nordplcdtype` | yes — Order Type | `ON PARTY` / `ON OFFICE`. EdifyBiz purchase orders are always on a supplier |
| 2 | `nordertype` | yes — Order Placed Type | `FRESH` / `AGAINST ORDER RECEIVED`. The real link to the Sales Order migrates instead (section 1.1 #18), which carries the same meaning |
| 3 | `ntooffice` | no | Used only for an order placed on an office |
| 4 | `bordmslandrol` | yes — Show Items that have reached MOL or ROL | A screen filter for picking items, not data |
| 5 | `bpendingpart` | yes — Show Parts Required of Service Calls | A screen filter, not data |
| 6 | `nprincpendingdays` | yes — Payment Pending Days | Read-only on the client screen; computed from the Payment Pending Since Date, which does migrate (section 2 #9) |
| 7 | `ncheckset` | yes — Check List | The check-list **set name** is not stored; its items migrate from `trdordpl5checks` (section 1.3) |
| 8 | `nitemtotal`, `ntotaldiscount`, `ntotaltaxamt`, `ntotalamount` | yes — Trn. Total | Read-only on the client screen. EdifyBiz recomputes it from the lines |
| 9 | `vletterhead`, `vletterheadlink` | yes — Select Letterhead to print | Print option of the eBizWiz report |
| 10 | `bapproval`, `approvedby`, `approvedon` | no | The client's order screen has no approval step |
| 11 | `ipaddress` | no | Audit only |

### 3.2 `trdordpl1items` (Items)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `ndiscount` | yes — Discount Amt. | Only 1 line of 31,364 has it. The discount is rebuilt from Master Rate − Order Rate instead (section 1.2) |
| 2 | `ntaxset`, `ntaxamt` | yes — Tax Set | A PO line has no tax column in EdifyBiz |
| 3 | `nquantitytotrecd` | yes — Recd Qty | Received quantity; EdifyBiz tracks receipts through its own GRN |
| 4 | `nquantitytotaccept`, `nquantitytotreject`, `nquantitytotmissing` | no | Accepted / rejected / missing quantities, no equivalent |
| 5 | `ncurrencyvalue` | yes — Currency Conversion Factor | eBizWiz already applied it; the stored rate is in the order currency |
| 6 | `vdiscountremarks` | yes — Remarks | Migrated to `pur_order_det.prod_desc` (section 1.2 #4) — listed here only because it is read by a separate query |
| 7 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Line audit; the order's dates are used on every line |

### 3.3 `trdordpl2deldate` (Delivery dates)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nquantity` | no | Quantity per delivery date; our form has one header date, not a schedule |
| 2 | `bdrop` | yes — Drop (items grid) | Line drop flag, no equivalent |
| 3 | `nordpl1` | no | Link to the order line |
| 4 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only. `ddeldate` itself migrates (section 1.1 #6) |

### 3.4 `trdordpl4posttaxchgs` (Post Tax Charges)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `bapplyonitemtotalonly`, `brunningposttax` | yes — Apply to Item Total Only, Running Total | The charges become one line of text (section 2 #14), so the calculation flags have nowhere to go |
| 2 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only |

### 3.5 `trdordpl5checks` (Check List)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nexpense` | no | `taskchecklist` has no expense column |
| 2 | `npendingreason` | no | `taskchecklist` has no pending-reason column. The **header's** Pending Reason does migrate (section 1.1 #11) |
| 3 | `addedby`, `editedby`, `ipaddress` | no | Audit only |

### 3.6 `trdordpl3payterms` (Payment Terms)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only. The data columns are folded into `pur_order.term` (section 2 #15) |
