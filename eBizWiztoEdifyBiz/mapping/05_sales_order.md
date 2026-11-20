# 05 — Sales Order

| | |
|---|---|
| **Client screen** | eBizWiz → `Order > Order Received`, and from **More**: Items, Post Tax Charges, Payment Terms, Terms & Cond., Check List, Expenses |
| **Our screen** | EdifyBiz → `Inquiry > Sales Order (SO)` |
| **Source tables** | `trhordrc` (header), `trdordrc1items` (items), `trdordrc2despdate` (despatch dates), `trdordrc3payterms` (payment terms), `trdordrc4posttaxchgs` (post tax charges), `trdordrc5checks` (check list), `trdordrc6expenses` (expenses), `trdordrc7tech` (technical details) |
| **Target tables** | `inqcs` (`cors='SO'`), `inqcsdet`, `inq_adjust`, `taskchecklist` (`module='SO'`) |
| **Code** | `Program.cs` → `static class SalesOrder` |
| **Runs** | After Customer Quotation, so the order can link back to its quotation and inquiry |

---

## 1. Migrated

### 1.1 Order Received header → `inqcs`  (main form)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Trn. No. | `trhordrc.vtrnprefix` + `ntrnno` | Order No. | `inqcs.inqref` | e.g. `OR3679` |
| 2 | Trn. Date | `trhordrc.dtrndate` | Order Date | `inqcs.inqdate` | |
| 2a | Valid Date | (none — defaulted) | Valid Date | `inqcs.validdate` = `inqcs.inqdate` | eBizWiz SO has no validity date; set equal to Order Date so the saksham current-date-locked Valid Date field is not empty when editing a migrated order |
| 3 | Order By | `trhordrc.nfromparty` → `contact` | Company Name | `inqcs.cscode` | The party the order came from. Resolved through the migrated Contact |
| 4 | Party Contact 1 + Party Contact 2 | `npartycontact`, `npartycontact2` → `mltcontact` | Contact Person | `inqcs.cperson` | Both contacts, comma-separated |
| 5 | Order Is | `trhordrc.nordrecdtype` → `mstfixedselection` | Order Type | `inqcs.ordertype` | `FRESH - SALES ORDER`, `AGAINST QUOTATION - SERVICE ORDER`, … Seeded into `miscellaneous(Inquiry / Order Type)` |
| 6 | Order Source | `trhordrc.nordsource` → `mstinquirysource` | Source | `inqcs.sources` | Name stored, not code |
| 7 | Pending Reason | `trhordrc.npendingreason` → `mstcallpendingreasons` | Pending Reason | `inqcs.pendingreason` | → `miscellaneous(Purchase Order / Pending Reason)`, the same list Purchase Order uses |
| 8 | Sales Person | `trhordrc.nsalesman` → `users` | Executive | `inqcs.executive` | Falls back to the migration user |
| 9 | Bill To | `trhordrc.nbillto` → `mltaddress` | Customer Billing Branch | `inqcs.csbranch` | Party's default address |
| 10 | Ship To | `trhordrc.nshipto` → `mltaddress` | Customer Shipping Branch | `inqcs.shippingbranch` | |
| 11 | Inquiry Category | `trhordrc.ninquirycategory` → `mstinquirycategory` | Category | `inqcs.Category` | → `miscellaneous(Inquiry / Category)` |
| 12 | Client Order No. | `trhordrc.vrefno` | PO Number | `inqcs.ponumber` | Capped 200 |
| 13 | Client Order Date | `trhordrc.drefdate` | PO Date | `inqcs.podate` | |
| 14 | Prn.Order Ackn. No. | `trhordrc.vordacknno` | Principal SO Number | `inqcs.sono` | Capped 100 |
| 14b | OrderTo (listing) | `trhordpl.ntoparty` of the Order Placed made against this OR (link: `trdordpl1items.nordrcditemdetailncode` → `trdordrc1items.ncode`; latest OP by `ncode`) → `contact` | OrderTo (list column) | `inqcs.principle` | Principal of the PO placed against this SO; blank when no OP exists. Listing `app/inquiry.asp` shows `contact.name` of `principle` (was wrongly `a.ordertype`). 5,710 / 7,918 orders have an OP; 25 multi-principal orders take the latest |
| 15 | Order Currency | `trhordrc.ncurrency` → `mstcurrency` | Currency | `inqcs.currency` | |
| 16 | — | `trhordrc.nexchangerate` | Exchange Rate | `inqcs.exchangerate` | |
| 17 | Order Status | `trhordrc.nordrecdstatus` → `mstfixedselection` | Status | `inqcs.status` | → `status` master |
| 18 | Remarks | `trhordrc.vremarks` | Remark | `inqcs.remark` | |
| 19 | — (quotation link) | `trhordrc.nquote` → migrated Quotation | Quotation No. | `inqcs.inqlink` | Resolved by the quotation's own Trn. No. |
| 20 | — (inquiry link) | `trhordrc.ninquiry` → migrated Inquiry | Inquiry No. | `inqcs.inqcode` | Resolved by the inquiry's own Trn. No. |
| 21 | (record office) | `trhordrc.nofficeid` | Branch | `inqcs.branchcode` | Office → `companyaddress` branch |
| 22 | (audit) | `addedon` / `addedby` | Created On / By | `inqcs.createdon` / `createdby` | |
| 23 | (audit) | `editedon` / `editedby` | Updated On / By | `inqcs.updatedon` / `updatedby` | Falls back to added on / by |

`inqcs.approvalstatus` is written as an empty string — the value the app itself posts on every save. The column default `Pending` would otherwise show every migrated order as awaiting approval, and the client's order screen has no approval step (`bapproval = 1` on all 7,658 orders).

### 1.2 Despatch date → `inqcs`  (per-line in eBizWiz, header field here)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Despatch Date | `trdordrc2despdate.ddespdate` | Customer Delivery Date | `inqcs.customertentativedate` | eBizWiz keeps one row per line, but all lines of an order carry the **same** date (4,954 of 4,954 orders with a date). Our form has it on the header, so the earliest date per order is used |

### 1.3 Order Received Items → `inqcsdet`  (Items screen, from More)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Item / Item Code | `trdordrc1items.nitem` → `product` | Product | `inqcsdet.pcode` | A line whose item does not resolve is skipped and counted |
| 2 | Quantity | `trdordrc1items.nquantity` | Quantity | `inqcsdet.quantity` | |
| 3 | Master Rate | `trdordrc1items.nmasterrate` | Price | `inqcsdet.price` | Used as the price when the line carries a discount |
| 4 | Order Rate | `trdordrc1items.nrate` | (net of discount) | — | Already net of the discount in eBizWiz. No discount → it becomes the Price |
| 5 | Discount % | `trdordrc1items.ndiscountperc` | Discount % | `inqcsdet.discountpercent` | |
| 6 | Discount Amt. | derived: Master Rate − Order Rate | Discount | `inqcsdet.discount` | Per unit, so qty × (price − discount) equals qty × Order Rate |
| 7 | Tax Set | `trdordrc1items.ntaxset` → `msttaxset` | Tax % | `inqcsdet.prodtax` | Percentage read out of the tax-set name. The document GST % wins when the header has one |
| 8 | Item Currency Is | `trdordrc1items.nitemcurrency` | Currency | `inqcsdet.currency` | The **order** currency is used; the item currency only when the order has none. eBizWiz rates are already converted |

### 1.4 Post Tax Charges → `inq_adjust`  (Post Tax Charges screen, from More)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Post Tax Charges | `trdordrc4posttaxchgs.nmstposttaxchgs` → `mstprepostchgs.vname` | Adjustment name | `inq_adjust.adjustname` | e.g. `Add : IGST @18%` |
| 2 | Percentage | `ntaxpercentage` | Percent | `inq_adjust.adjustpercent` | |
| 3 | Amount | `namount` | Amount | `inq_adjust.adjustamount` | |
| 4 | Amount / Percentage | `venteredinamtorper` | — | — | A PERCENTAGE charge is applied on the item total (qty × Order Rate of every line); an AMOUNT charge is taken as entered |

Two extra adjustments keep the EdifyBiz total equal to the eBizWiz total:

| Adjustment | When | Why |
|---|---|---|
| `Other items (eBizWiz)` | item total ≠ sum of migrated lines | Blank-item and unmapped lines cannot sit on a migrated line |
| `Tax on items (eBizWiz)` | any line carried a Tax Set amount | A per-line tax amount has no column on a migrated line |

### 1.5 Check List → `taskchecklist` (`module='SO'`)  (Check List Entry / Task Check List tab)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Check | `trdordrc5checks.ncheck` → `mstchecks.vname` | Title | `taskchecklist.title` | Falls back to `Check <code>` |
| 2 | Done | `bdone` | Status | `taskchecklist.status` | Done → 1, else 0 |
| 3 | Expected Date | `dexpdate` | Due Date | `taskchecklist.duedate` | Empty → done date, else a future date |
| 4 | Alloted To | `nallotedto` → `users` | Assign To | `taskchecklist.assignto` | |
| 5 | Remarks | `vremarks` | Remark | `taskchecklist.remark` | |
| 6 | (grid order) | `ncode` | — | `taskchecklist.sort` | |
| 7 | (audit) | `addedon` / `editedon` | Created / Updated On | `taskchecklist.createdon` / `updatedon` | |

No `task` record is created — the items live in the Task Check List tab only.

### 1.6 Masters seeded

| Source | Target |
|---|---|
| `mstfixedselection` (`nordrecdstatus`) | `status` |
| `mstfixedselection` (`nordrecdtype`) | `miscellaneous(Inquiry / Order Type)` |
| `mstinquirycategory` | `miscellaneous(Inquiry / Category)` |
| `mstcallpendingreasons` | `miscellaneous(Purchase Order / Pending Reason)` — shared with Purchase Order |
| `mstcurrency` | `currency` |

---

## 2. Folded into a text field

| # | Client UI | Client table.column | Lands in | Written as |
|---|---|---|---|---|
| 1 | Comment | `trhordrc.vcomment` | `inqcs.remark` | `Comment: <value>` |
| 2 | Prn.Order Ackn. Date | `trhordrc.dackndate` | `inqcs.remark` | `Order Ackn Date: dd/MM/yyyy` — the number has its own field, the date does not |
| 3 | Expenses (whole screen) | `trdordrc6expenses` | `inqcs.remark` | `Expenses: <type> <date> <amount> <remarks>; …`, one entry per row. Source holds **318 rows** |
| 4 | Payment Terms (whole screen) | `trdordrc3payterms` → `mstpaymentterms.vname` | `inqcs.terms` | `Payment Terms: <term> <percent>% = <amount>; …`, one entry per row |

---

## 3. Skipped

Every column below was searched across the whole of `Program.cs`, not just this module — none is read anywhere.

### 3.1 `trhordrc` (header)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nordrecdfrom` | yes — Order From | `FROM PARTY` on 7,911 of 7,918 orders, so it carries no information. The party itself migrates as the customer |
| 2 | `nfromuser`, `nfromoffice`, `ntooffice` | no | Used only when the order is from a user or an office; 6 rows in all |
| 3 | `nterms` | yes — Terms & Cond. | Selected but never written. Only the Payment Terms grid reaches `inqcs.terms` (section 2 #4) |
| 4 | `vtermsconditions` | no | Free-text terms, no equivalent |
| 5 | `ncheckset` | yes — Check List | The check-list **set name** is not stored; its items migrate from `trdordrc5checks` (section 1.5) |
| 6 | `nitemtotal`, `ntotaldiscount`, `ntotaltaxamt`, `ntotalamount` | yes — Trn. Total | Read-only on the client screen. EdifyBiz recomputes from the lines plus the adjustments in section 1.4 |
| 7 | `vletterhead`, `vletterheadlink` | yes — Select Letterhead to print | Print option of the eBizWiz report |
| 8 | `nordno` | no | Internal order number |
| 9 | `nscheme`, `nschemeqty`, `nschemediscount` | no | Scheme discounts, not used by this client |
| 10 | `vspnumber`, `nbillingfrom`, `nbusigroup`, `nosreason` | no | SP number, billing office, business group, order-status reason — no EdifyBiz equivalent |
| 11 | `bdod`, `bcod`, `bcashadvance`, `nadvance`, `vchqno`, `dchqdate`, `nbank` | no | Advance / cheque details; payments live in the Payment module |
| 12 | `bentrytax`, `bformc`, `bformd`, `btaxinclusive`, `broadpermit`, `boctapplicable`, `boctbyipl`, `bmatoutoctlimit` | no | Pre-GST tax and octroi flags |
| 13 | `ncommamt`, `nthirdparty`, `ncommtosalesman` | no | Commission figures, no equivalent |
| 14 | `bneworder`, `bupgradation` | no | Order classification flags |
| 15 | `bapproval`, `approvedby`, `approvedon` | no | The client's order screen has no approval step |
| 16 | `ipaddress` | no | Audit only |

### 3.2 `trdordrc1items` (Items)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `ntechnicalset` | yes — Technical Set | Technical specification sets are not migrated |
| 2 | `ndiscount` | yes — Discount Amt. | Recomputed as Master Rate − Order Rate so the net stays identical (section 1.3 #6) |
| 3 | `ncurrencyvalue` | yes — Currency Conversion Factor | eBizWiz already applied it; the stored rates are in the order currency |
| 4 | `nquantitytotdesp` | yes — Disp. Qty. | Despatched quantity; EdifyBiz tracks despatch in its own Dispatch Advice |
| 5 | `nquantitytotreject` | no | Rejected quantity, no equivalent |
| 6 | `vdiscountremarks` | no | Line discount remark, no equivalent on a migrated line |
| 7 | `busebom` | no | Bill-of-material flag |
| 8 | `nscheme`, `nschemeqty`, `nschemediscount` | no | Line scheme discounts |
| 9 | `nlocalsupplier`, `nlocalprice`, `nbranchtrfval` | no | Local supplier / branch transfer values |
| 10 | `nordplcdqty` | no | Quantity already ordered on a Purchase Order |
| 11 | `noemparty`, `nprice` | no | OEM party and an unused price column |
| 12 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Line audit; the order's dates are used on every line |

### 3.3 `trdordrc2despdate` (Despatch dates)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nquantity` | no | Quantity per despatch date; our form has one header date, not a schedule |
| 2 | `bdrop` | yes — Drop (items grid) | Line drop flag, no equivalent |
| 3 | `nordrc1` | no | Link to the order line |
| 4 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only |

### 3.4 `trdordrc4posttaxchgs` (Post Tax Charges)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `brunningposttax` | yes — Apply to Running Post Tax Total Only | `inq_adjust` has no running-total flag. Applying every percentage charge on the item total matches more documents than a running base |
| 2 | `bapplyonitemtotalonly` | yes — Apply to Item Total Only | Read while calculating, but not stored on the adjustment |
| 3 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only |

### 3.5 `trdordrc5checks` (Check List)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nexpense` | no | `taskchecklist` has no expense column |
| 2 | `npendingreason` | no | `taskchecklist` has no pending-reason column. The **header's** Pending Reason does migrate (section 1.1 #7) |
| 3 | `addedby`, `editedby`, `ipaddress` | no | Audit only |

### 3.6 `trdordrc3payterms` (Payment Terms) and `trdordrc6expenses` (Expenses)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only. The data columns are folded into text (section 2 #3 and #4) |

### 3.7 `trdordrc7tech` (Technical details)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | whole table | yes — Tech. Details (per item) | Technical records are not migrated. The source holds **4 rows** in all four offices |
