# 04 — Customer Quotation

| | |
|---|---|
| **Client screen** | eBizWiz → `Quotation > Quotation Entry`, and from **More**: Items, Post Tax Charges, Payment Terms, Terms & Cond., Check List |
| **Our screen** | EdifyBiz → `Inquiry > Customer Quotation (CQ)` |
| **Source tables** | `trhquote` (header), `trdquote1items` (items), `trdquote2payterms` (payment terms), `trdquote3posttaxchgs` (post tax charges), `trdquote5checks` (check list), `trdquote4tech` (technical details) |
| **Target tables** | `inqcs` (`cors='CQ'`), `inqcsdet`, `inq_adjust`, `taskchecklist` (`module='CQ'`) |
| **Code** | `Program.cs` → `static class CustomerQuotation` |
| **Runs** | After Customer Inquiry, so the quotation can link back to its inquiry |

---

## 1. Migrated

### 1.1 Quotation Entry header → `inqcs`  (main form)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Trn. No. | `trhquote.vtrnprefix` + `ntrnno` | Quotation No. | `inqcs.inqref` | Joined and trimmed to `QT38229`, not `QT38229.00` |
| 2 | Trn. Date | `trhquote.dtrndate` | Quotation Date | `inqcs.inqdate` | |
| 3 | Party | `trhquote.nparty` → `contact` | Company Name | `inqcs.cscode` | Resolved through the migrated Contact; its default address goes to `inqcs.csbranch` |
| 4 | Party Contact | `trhquote.npartycontact` → `mltcontact` | Contact Person | `inqcs.cperson` | Source person number resolved to the migrated `mltcontact.code` |
| 5 | Quote Type | `trhquote.nquotetype` → `mstfixedselection` | Quote Type | `inqcs.quotetype` | → `miscellaneous(Inquiry / Quote Type)` |
| 6 | Inquiry No. | `trhquote.ninquiry` → migrated Inquiry | Inquiry No. | `inqcs.inqlink` | Resolved by the inquiry's own Trn. No., so the two documents stay linked |
| 7 | Sales Person | `trhquote.nsalesman` → `users` | Executive | `inqcs.executive` | Falls back to the migration user |
| 8 | Follow Up Date | `trhquote.dfollowupdate` | Follow Up Date | `inqcs.followupdate` | |
| 9 | Quote Currency | `trhquote.ncurrency` → `mstcurrency` | Currency | `inqcs.currency` | |
| 10 | — | `trhquote.nexchangerate` | Exchange Rate | `inqcs.exchangerate` | |
| 11 | Quote Status | `trhquote.nquotestatus` → `mstfixedselection` | Status | `inqcs.status` | → `status` master |
| 12 | Lost Reason | `trhquote.nlostreasons` → `mstorderlostreason` | Lost Reason | `inqcs.lossreason` | → `miscellaneous(Inquiry / Loss Reason)` |
| 13 | Ref. No. | `trhquote.vrefno` | Enquiry Ref No | `inqcs.enqrefno` | Capped 200 |
| 14 | Ref. Date | `trhquote.drefdate` | Enquiry Ref Date | `inqcs.enqrefdate` | |
| 15 | Inquiry Category | `trhquote.ninquirycategory` → `mstinquirycategory` | Category | `inqcs.Category` | → `miscellaneous(Inquiry / Category)` |
| 16 | Remarks | `trhquote.vremarks` | Remark | `inqcs.remark` | |
| 17 | (record office) | `trhquote.nofficeid` | Branch | `inqcs.branchcode` | Office → `companyaddress` branch |
| 18 | (audit) | `addedon` / `addedby` | Created On / By | `inqcs.createdon` / `createdby` | |
| 19 | (audit) | `editedon` / `editedby` | Updated On / By | `inqcs.updatedon` / `updatedby` | Falls back to added on / by |

`inqcs.approvalstatus` is written as `Approved` — the column defaults to `Pending`, which would list every migrated quotation under "Approval Pending". eBizWiz has `bapproval = 1` on all of them.

### 1.2 Quotation Items → `inqcsdet`  (Items screen, from More)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Item Name / Item Code | `trdquote1items.nitem` → `product` | Product | `inqcsdet.pcode` | A line whose item does not resolve is skipped and counted |
| 2 | Quantity | `trdquote1items.nquantity` | Quantity | `inqcsdet.quantity` | |
| 3 | Master Rate | `trdquote1items.nmasterrate` | Price | `inqcsdet.price` | Used as the price when the line carries a discount |
| 4 | Quote Rate | `trdquote1items.nrate` | (net of discount) | — | Already net of the discount in eBizWiz. No discount → it becomes the Price |
| 5 | Discount % | `trdquote1items.ndiscountperc` | Discount % | `inqcsdet.discountpercent` | |
| 6 | Discount Amt. | derived: Master Rate − Quote Rate | Discount | `inqcsdet.discount` | Per unit, so qty × (price − discount) equals qty × Quote Rate |
| 7 | Tax Set | `trdquote1items.ntaxset` → `msttaxset` | Tax % | `inqcsdet.prodtax` | Percentage read out of the tax-set name. The document GST % wins when the header has one |
| 8 | Item Currency Is | `trdquote1items.nitemcurrency` | Currency | `inqcsdet.currency` | The **quotation** currency is used; the item currency only when the quotation has none. eBizWiz rates are already converted into the quote currency |

### 1.3 Post Tax Charges → `inq_adjust`  (Post Tax Charges screen, from More)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Post Tax Charges | `trdquote3posttaxchgs.nmstposttaxchgs` → `mstprepostchgs.vname` | Adjustment name | `inq_adjust.adjustname` | e.g. `Less : Discount`, `Add : Packing & Forwarding Charges` |
| 2 | Percentage | `ntaxpercentage` | Percent | `inq_adjust.adjustpercent` | |
| 3 | Amount | `namount` | Amount | `inq_adjust.adjustamount` | |
| 4 | Amount / Percentage | `venteredinamtorper` | — | — | Decides the calculation: a PERCENTAGE charge is always applied on the item total (qty × Quote Rate of every line); an AMOUNT charge is taken as entered |

Two extra adjustments are added so the EdifyBiz total matches the eBizWiz total exactly:

| Adjustment | When | Why |
|---|---|---|
| `Other items (eBizWiz)` | item total ≠ sum of migrated lines | Blank-item and unmapped lines cannot sit on a migrated line |
| `Tax on items (eBizWiz)` | any line carried a Tax Set amount | A per-line tax amount has no column on a migrated line |

### 1.4 Check List → `taskchecklist` (`module='CQ'`)  (Check List screen, from More / Task Check List tab)

| # | Client UI | Client table.column | Our UI | Our table.column | Note |
|---|---|---|---|---|---|
| 1 | Check | `trdquote5checks.ncheck` → `mstchecks.vname` | Title | `taskchecklist.title` | Falls back to `Check <code>` |
| 2 | Done | `bdone` | Status | `taskchecklist.status` | Done → 1, else 0 |
| 3 | Expected Date | `dexpdate` | Due Date | `taskchecklist.duedate` | Empty → done date, else today + 7 days |
| 4 | Alloted To | `nallotedto` → `users` | Assign To | `taskchecklist.assignto` | |
| 5 | Remarks | `vremarks` | Remark | `taskchecklist.remark` | |
| 6 | (grid order) | `ncode` | — | `taskchecklist.sort` | |
| 7 | (audit) | `addedon` / `editedon` | Created / Updated On | `taskchecklist.createdon` / `updatedon` | |

### 1.5 Masters seeded

| Source | Target |
|---|---|
| `mstfixedselection` (`nquotestatus`) | `status` |
| `mstfixedselection` (`nquotetype`) | `miscellaneous(Inquiry / Quote Type)` |
| `mstinquirycategory` | `miscellaneous(Inquiry / Category)` |
| `mstorderlostreason` | `miscellaneous(Inquiry / Loss Reason)` |
| `mstcurrency` | `currency` |

---

## 2. Folded into a text field

| # | Client UI | Client table.column | Lands in | Written as |
|---|---|---|---|---|
| 1 | Comment | `trhquote.vcomment` | `inqcs.remark` | `Comment: <value>` |
| 2 | Validity | `trhquote.vvalidity` | `inqcs.remark` | `Validity: <value>` |
| 3 | Signatory | `trhquote.nsignatory` → `mstusers.vname` | `inqcs.remark` | `Signatory: <name>` |
| 4 | Payment Terms (whole screen) | `trdquote2payterms` → `mstpaymentterms.vname` | `inqcs.terms` | `Payment Terms: <term> <percent>% <amount>; …`, one entry per row |
| 5 | Terms & Cond. | `trhquote.nterms` → `msttermset.vname` | `inqcs.terms` | `Terms & Cond.: <set name>` |

The Payment Terms screen has no grid of its own in EdifyBiz, so all three of its columns — Pay Term, Percentage, Amount — become one line of text in the quotation's Terms field.

---

## 3. Skipped

Every column below was searched across the whole of `Program.cs`, not just this module — none is read anywhere.

### 3.1 `trhquote` (header)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nitemtotal`, `ntotalamount` | yes — Trn. Total | Read-only on the client screen. EdifyBiz recomputes it from the lines plus the adjustments in section 1.3, which is why the two reconciling adjustments are added |
| 2 | `ndiscount`, `ntaxset` | no | Header-level discount and tax set; the real values come from the lines and the post-tax charges |
| 3 | `ncheckset` | yes — Check List | The check-list **set name** is not stored; its items migrate from `trdquote5checks` (section 1.4) |
| 4 | `bitemset` | yes — Quote For Item Groups | Item-group quoting is not migrated |
| 5 | `bshowcurrency` | yes — Show Items Of Selected Currency | A screen filter, not data |
| 6 | `bPrintwithPrice`, `bPrintwithTerm`, `bPrintwithTerms`, `bPrintwithoutPrice`, `bPrintwithoutTerms` | yes — Print with Price, Print with Terms | Print options of the eBizWiz report; EdifyBiz has its own print settings |
| 7 | `vletterHead`, `vletterhead1`, `vletterheadlink` | yes — Select Letterhead to print | Letterhead choice belongs to the eBizWiz report |
| 8 | `vtermsconditions` | no | Free-text terms; only the Terms & Cond. **set name** migrates (section 2 #5) |
| 9 | `nbillingoffice` | no | Billing office of the quotation, no EdifyBiz equivalent |
| 10 | `bapproval`, `approvedby`, `approvedon` | no | All quotations are approved in eBizWiz; `approvalstatus` is set to `Approved` instead |
| 11 | `ipaddress` | no | Audit only |

### 3.2 `trdquote1items` (Items)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nitemset` | yes — Item Group Heading | Item groups are not migrated |
| 2 | `ntechnicalset` | yes — Technical Set | Technical specification sets are not migrated |
| 3 | `ndiscount` | yes — Discount Amt. | Recomputed as Master Rate − Quote Rate so the net stays identical (section 1.2 #6) |
| 4 | `ncurrencyvalue` | yes — Currency Conversion Factor | eBizWiz already applied it; the stored rates are in the quote currency |
| 5 | `vdescription` | no | Line description, no equivalent on a migrated line |
| 6 | `noemparty` | no | OEM party per line, no equivalent |
| 7 | `nprice` | no | Unused price column; Master Rate and Quote Rate carry the real values |
| 8 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Line audit; the quotation's dates are used on every line |

### 3.3 `trdquote3posttaxchgs` (Post Tax Charges)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `brunningposttax` | yes — Apply to Running Post Tax Total Only | `inq_adjust` has no running-total flag. Verified against `ntotalamount`: applying every percentage charge on the item total matches more documents than a running base |
| 2 | `bapplyonitemtotalonly` | yes — Apply to Item Total Only | Read while calculating, but not stored on the adjustment |
| 3 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only |

### 3.4 `trdquote5checks` (Check List)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `nexpense` | no | `taskchecklist` has no expense column |
| 2 | `npendingreason` | no | `taskchecklist` has no pending-reason column |
| 3 | `addedby`, `editedby`, `ipaddress` | no | Audit only |

### 3.5 `trdquote2payterms` (Payment Terms)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | `addedby`, `addedon`, `editedby`, `editedon`, `ipaddress` | no | Audit only. The three data columns are folded into `inqcs.terms` (section 2 #4) |

### 3.6 `trdquote4tech` (Technical details)

| # | Client table.column | On client screen | Why |
|---|---|---|---|
| 1 | whole table | yes — Add Tech. Details (per item) | Technical records are not migrated. The source holds **15 rows** in all four offices |
