# 09. Stock In / Stock Out → GRN / MRO / GTA

Format: see `00_MAPPING_FORMAT.md`.

## 1. Overview

| | Client (eBizWiz) | Ours (EdifyBiz) |
|---|---|---|
| Screen / menu | **Stock In** (Stock In From: FROM PARTY / FROM OFFICE / FROM USER; Stock In Is: FRESH / AGAINST ORDER PLACED / AGAINST STOCK OUT / AGAINST W SALES RETURN) and **Stock OUT** (Stock Out To: TO PARTY / TO OFFICE / TO USER); header + *Stock In Item* / *Stock Out Item* (with serial numbers) | **Inventory** module: **GRN** `/edify/inventory/grn/` (stock in), **MRO** `/edify/inventory/mro/` (stock out), **GTA** `/edify/inventory/gta/` (branch transfer), Stock Summary report |
| Tables | `trhstkin` + `trdstkin1items` (+ `trdstkin2posttaxchgs`, `trdstkin3checks`, `trdstkin4payterms`, `trdstkin5serialno`), `trhstkou` + `trdstkou1items` (+ `trdstkou2posttaxchgs`, `trdstkou3checks`); masters `mstfixedselection` (from / to / type / dispatch mode), `mstparty`, `mstusers`, `mstoffice`, `mstitems` | `stockinout` (`type` GRN / MRO) + `stockinoutdet`, `stockb2b` + `stockb2bdet`, ledger `stocktrans`, `productstocksummary`; master `miscellaneous` (GRN / grntype) |
| Code | — | `inventory/{grn,mro,gta}/default.asp`, `assets/scripts/inventory/{grn,mro,gta}.js`, `app/inventory/{grn,mro,gta}.asp`, stock postings `app/inventory/inventoryfunction.asp` |
| Exe | — | module **8. Stock** — class `Stock` in `Program.cs` (after modules 1–7: products, POs and invoices must exist) |
| Scope | offices 2, 3, 4, 6 (1 and 5 are WinMax test offices) — Stock In 7,150, Stock Out 1,326 | |
| Status | | migrated and verified |

**No form change:** the GRN, MRO and GTA screens are used as they are. Client fields that have no field on them are kept, labelled, in the document's Remarks tab.

---

## 2. Field mapping

### 2.1 Which screen a client document goes to

| Client Stock In From / Out To | Client type | Docs | Ours |
|---|---|---|---|
| FROM PARTY | AGAINST ORDER PLACED | 4,759 | **GRN** with the PO link |
| FROM PARTY | FRESH | 1,206 | **GRN** |
| FROM PARTY | AGAINST W SALES RETURN | 6 | **GRN** |
| FROM USER | FRESH | 392 | **GRN** at the office branch (engineer returns stock) |
| FROM OFFICE | with a matching Stock Out of the same items | 717 | inside the **GTA** of that Stock Out |
| FROM OFFICE | pair with different items, or no Stock Out | 70 | **GRN** |
| TO OFFICE | received by a Stock In with the same items | 717 | **GTA** (not in transit) |
| TO OFFICE | not received | 50 | **GTA in transit** (stock at branch 0) |
| TO OFFICE | received with different items | 25 | **MRO** (+ the Stock In as GRN) |
| TO USER | FRESH | 526 | **MRO** at the office branch (engineer issue) |
| TO PARTY | FRESH / AGAINST ORDER RECEIVED / AGAINST CLAIM | 8 | **MRO** |

### 2.2 Stock In header → `stockinout` (`type = 'GRN'`)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 1 | Trn. No. | `trhstkin.vtrnprefix` + `ntrnno` | `stockinout.number` | GRN Number | GRN Number | e.g. `SI3222` |
| 2 | Trn. Date | `dtrndate` | `stockinout.date`, `createdon` | GRN Date | GRN Date | SI2384 has 2202-11-10 in the client → its entry date |
| 3 | From (party) | `nfromparty` → `mstparty` | `stockinout.scode` | Supplier | Supplier | party → contact (module 1) |
| 4 | From (user) | `nfromuser` → `mstusers` | part of `remark` | Remarks | Remarks tab | Supplier blank; `Stock-IN FROM USER … User: <name>` |
| 5 | From (office) | `nfromoffice` | part of `remark` / GTA | Remarks | Remarks tab | see 2.1 |
| 6 | Stock In Is | `nstkintype` → `mstfixedselection` | `stockinout.grntype` | Type | Type | name as text; full group seeded |
| 7 | (against order placed) | line `nordpl1` → `trdordpl1items` → PO | `stockinout.module = 'PO'`, `modulecode = pur_order.code` | PO/PI + PO/PI Number | Invoice Number | 4,754 GRNs; a GRN against several POs keeps the first, the others go to the remark (`Against POs: …`) |
| 8 | (office of the record) | `nofficeid` | `stockinout.branchcode` | Branch Name | Branch Name | office → branch 3/4/5/6 |
| 9 | Dispatch Mode, Dispatch Through, Docket No., Docket Date | `ndispatchmode`, `vdespatchthru`, `vdespdocno`, `ddespdocdate` | part of `remark` | Remarks | Remarks tab | no dispatch / transporter / LR field on the GRN (vehicle no. only) |
| 10 | Ref. No. / Ref. Date | `vrefno`, `drefdate` | part of `remark` | Remarks | Remarks tab | the supplier reference fields on the GRN are Bendy-only |
| 11 | Remarks / Comment | `vremarks`, `vcomment` | `remark` | Remarks | Remarks tab | |
| 12 | Post Tax Charges | `trdstkin2posttaxchgs` (65) | part of `remark` | Remarks | Remarks tab | `Charges: …`; inventory documents carry no amounts |
| 13 | Missing qty (lines) | `trdstkin1items.nquantitymissing` (212 lines) | part of `remark` | Remarks | Remarks tab | `Missing qty: …`; not stock |
| 14 | Payment Terms | `trdstkin4payterms` (1 row) | part of `remark` | Remarks | Remarks tab | `Payment Terms: <term> 100% = 1073.00` (office 3, Stock-IN 104); a GRN has no terms field |
| 15 | Serial numbers | `trdstkin5serialno` (22) | part of `remark` | Remarks | Remarks tab | `Serial No: …`; the GRN has no serial entry (serials live only in the batch master, without a GRN link) |
| 16 | Terms & Cond. / Check List | `nterms`, `ncheckset`, `trdstkin3checks` | — | — | — | 0 / 0 / 0 rows |
| 17 | Trn. Total | `ntotalamount` | — | — | — | no amounts on inventory documents |

### 2.3 Stock In items → `stockinoutdet` + ledger

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 18 | Item Name / Item Code | `trdstkin1items.nitem` | `stockinoutdet.productcode` | Product | Product | product by Item Code (module 2, `product.casno`) |
| 19 | — | — | `stockinoutdet.prodbatchcode` | Batch | Batch | the product's Default Batch (batch is required) |
| 20 | Qty. Accepted (Good) | `nquantityaccept` | `stockinoutdet.qty` | Quantity | Quantity | good stock |
| 21 | Qty. Rejected (Def.) | `nquantityreject` | `stockinoutdet.dqty` | Damaged Qty | Damaged Qty. | defective stock (325 lines) |
| 22 | Stock In Rate | `nrate` | `stockinoutdet.price` | Price | Price | |
| 23 | — | item unit | `stockinoutdet.unit` | Unit | Unit | `product.unitcode` |
| 24 | Challan Qty / Qty. Received / Master Rate / Discount / Tax Set / Reject Reason / Auto Generated | `nquantity`, `nquantityrecd`, `nmasterrate`, `ndiscount*`, `ntaxset`, `nrejectreason`, `bautoserial` | — | — | — | no such field on a GRN line |
| 25 | (stock ledger) | — | `stocktrans` (`module` GRN, `modulecode` = `stockinoutdet.code`, +qty / +dqty, date = document date) | — | Stock Summary | same row the app's `InventoryTrigger` writes |

### 2.4 Stock Out → `stockinout` (`type = 'MRO'`) / `stockb2b` (GTA)

| # | Client UI | Client DB | Our DB | Our Form | Our View | Rule |
|---|---|---|---|---|---|---|
| 26 | Trn. No. / Trn. Date | `trhstkou.vtrnprefix` + `ntrnno`, `dtrndate` | MRO `number` / `date`; GTA `stockb2b.number` / `date` | MRO / GTA Number, Date | same | |
| 27 | To (party) | `ntoparty` | MRO `scode` | Contractor | Contractor | |
| 28 | To (user) | `ntouser` | part of MRO `remark` | Remarks | Remarks tab | Contractor blank; `User: <name>` |
| 29 | To (office) | `ntooffice` | GTA `tobranchcode` (`frombranchcode` = own office) | To / From Branch Name | same | not received → `intransit = 1` |
| 30 | Stock Out Is | `nstkoutype` | MRO: part of `remark`; GTA: not written (all 792 GTA are FRESH) | Remarks | Remarks tab | MRO / GTA have no type for this; GTA Type allows only Chargeable / Non-Chargeable |
| 31 | Dispatch Mode / Through, Docket No. / Date, Ref. No. / Date, Remarks, Comment, charges | as 2.2 | part of `remark` | Remarks | Remarks tab | GTA "Transporter" is a contact picker, the client value is free text |
| 32 | (paired Stock In of a GTA) | `trhstkin.nstkouno` | part of GTA `remark` | Remarks | Remarks tab | `Received by Stock-IN SI…` |
| 33 | Items: Item, Quantity, Defective qty, Rate | `trdstkou1items.nitem`, `nquantity`, `ndefquantity`, `nrate` | `stockinoutdet` / `stockb2bdet` (`productcode`, Default Batch, `qty`, `dqty`, `price`, `unit`) | Product, Batch, Quantity, Damaged Qty, Price, Unit | same | |
| 33a | Line links: Stock-IN `trdstkin1items.nsales1` (5 lines), Stock-OUT `trdstkou1items.nordrc1` (6 lines) | — | not migrated | — | — | the GRN / MRO "Invoice Number" reads only `pur_order`; there is no line-level sales / order link |
| 34 | (stock ledger) | — | `stocktrans` MRO −qty / −dqty; GTA −qty at the from-branch, + at the to-branch (branch 0 in transit) | — | Stock Summary | `dqty` written negative when stock leaves (the app would add it) |

### 2.5 Movements from other modules (same ledger)

| # | Source | Our DB | Rule |
|---|---|---|---|
| 35 | Sales Invoice lines (module 7) | `stocktrans` `SAL` −qty, `modulecode` = `sal_order_det.code` | not for service products or qty 0 — as the app's `SalesInventoryTrigger` |
| 36 | Spares used on service calls (module 11) | one MRO per call + `stocktrans` MRO | non-service items only (LABOUR rows stay in Used Product) |
| 37 | Item Opening Balance [Office-wise] (`mststkdt.ngoodopbal` / `ndefopbal` on `dopbaldate`) | `stockjv` (module `JV`, remark "Opening Stock", price = product price) + `stocktrans` `JV` +qty / +dqty, `modulecode` = `stockjv.code` | EdifyBiz standard **Inventory > Opening Stock** (`app/inventory/openingstock.asp` → `InventoryTrigger` JV); 2,590 office + item rows, good 337,749, damaged 1 row |
| 38a | Client balance (`mststkdt.ngoodbal` / `ndefbal`) after every movement (end of module 11) | `stockjv` (module `JV`, remark "eBizWiz balance adjustment", date = `mststkdt.editedon`) + `stocktrans` `JV` ± qty / ± dqty | one standard stock JV per branch + product for the difference between the client balance and the ledger (the client edits balances directly in Change Stock Details, which leaves no transaction); service products skipped. 2,501 rows, good +396 / −35,361, damaged +3 |
| 38 | After all movements | `productstocksummary` | rebuilt from `stocktrans` per branch + product + batch |

---

## 3. Masters seeded

| Master | Our table | Rows added | Source |
|---|---|---|---|
| GRN Type | `miscellaneous` (GRN / grntype) | 9 (full group) | `mstfixedselection` `nstkintype` |

---

## 4. Changes made on our side

### 4a. Form / view changes

None — the GRN, MRO and GTA forms and views are used as they are. User branch access, which these lists depend on, is set by module 1 (01 §3).

**New fields: 0**

### 4b. Database changes (ALTER)

None.

---

## 5. Not migrated

| Client UI | Client DB | Rows | Why |
|---|---|---|---|
| Physical Stock Taking | `trhpstk` + `trdpstk1detail` | 5 headers, 0 lines | nothing to post |
| Stock transfer user → user | `trhstktrf` | 1 (in scope), 0 lines | no branch change |
| Serial master entries | `trdstkin5serialno` | 22 | kept in the GRN remark (row 15); our serial master (`prodbatchsrno`) has no GRN link and the sold serials of module 7 are not in it either |

---

## 6. Verification (latest run)

Run ALL, 0 errors:

| Check | Client | Ours |
|---|---|---|
| Stock In | 7,150 | **6,433** GRN + **717** received inside a GTA |
| Stock Out | 1,326 | MRO **559** (526 to user + 8 to party + 25 pairs with different items) + GTA **767** (50 in transit) |
| MRO total | — | **665** = 559 + 106 spare-part MROs of module 11 |
| GRN → PO | 4,754 GRNs against an order | **4,754** `module = 'PO'` with the PO code |
| Ledger | — | GRN 29,007 rows (+81,883 / damaged +571), MRO 1,021 (−2,080 / −9), GTA 3,190 (net 0), SAL 18,629 (−40,508); every row points to an existing line |
| **Closing stock** per branch + item = client (**opening** + Stock In accepted − Stock Out − sold − call spares), good and damaged | 5,652 office + item keys with a balance (`mststkdt.ngoodbal` / `ndefbal`) | **4,352 / 4,352** stock-product keys equal the client balance: good **341,766 = 341,766**, damaged **663 = 663** (2,501 "eBizWiz balance adjustment" JVs, good +396 / −35,361, damaged +3). Negative balances 118 rows / −546 on both sides. Service items (17 keys, client 310 units) hold no stock in EdifyBiz |
| Summary vs ledger | — | `productstocksummary` 6,367 rows = `stocktrans` on 6,367 of 6,367 |
| Negative closing stock | client stock also negative on the same items | 282 rows |

---

## 7. Notes

- **Created By / Updated By** on the migrated document = the eBizWiz `addedby` / `editedby` user (migration user only when the eBizWiz user is unknown, and on rows the migration generates itself, such as AMC bill invoices, service-call invoices and the spare-part MRO). Verified in the DB on 23/09/2026.
- **Visibility.** GRN / MRO / GTA lists and views show only documents whose branch is in the user's `users.companybranch`. Module 1 fills it from the client's user–office access (`msduseroffice`) and gives Admin (the migration user) all four branches, so every user sees his offices.
- **Why so much goes to Remarks.** The GRN has number, date, supplier, branch, PO link, vehicle no., type and project; the MRO has number, date, contractor, invoice link, branch and project; the GTA has from / to branch, in transit, contractor, type, transporter (contact), vehicle, e-way bill and project. The client's dispatch mode / through, docket no / date, reference no / date, user, charges, payment terms, missing qty and serial numbers have no field there, and the forms are not changed.
- **MRO without Contractor** (657: issues to users and call spares): the MRO form requires a Contractor when the header is edited.
- **App view quirks (standard screens, left as they are):** the GRN view looks up "GTA No" by `modulecode` without checking `module`, so 502 PO GRNs whose PO code equals a GTA code show that GTA number; the GTA view's "GRN No" has the same kind of join.
- **Stock checks on edit.** MRO / GTA header edits re-check every line against `productstocksummary`, which is why the summary is rebuilt after all modules that move stock (7, 8, 11).
- Transfers are matched pair by pair (`trhstkin.nstkouno`): same items → one GTA, so each branch moves exactly what the client moved; different items → the Stock Out as MRO and the Stock In as GRN.
- **Closing stock = the client balance.** The client balance can be edited directly in eBizWiz (Stock > Change Stock Details), which leaves no transaction, so the ledger (opening + in − out − sold − spares) differed on 2,501 keys (e.g. Cryo Box 2 Inch HO: 1,581 received, nothing issued, balance 0). Row 38a posts one "eBizWiz balance adjustment" stock JV per key, the same standard entry as Opening Stock, dated when the client last changed the balance; the full transaction history stays as migrated.
- The client keeps its balance per office + item in `mststkdt` (`ngoodbal`, `ndefbal`) = **opening** (`ngoodopbal`, `ndefopbal`) + in − out − sold − call spares. Of the 2,590 rows with an opening balance, 1,901 match this formula only when the opening is added (2 without it), so the opening stock is migrated (row 37) and the closing-stock check must include it.
