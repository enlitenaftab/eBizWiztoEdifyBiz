# Stock-IN / Stock-OUT / Physical Stock — Migration Mapping (eBizWiz → EdifyBiz Inventory)

Client menu **Order → Stock**: Stock-IN, Stock-OUT, Physical Stock Taking. Read as: what the client has → where it sits in the client DB → which table/column it goes to in our DB.

EdifyBiz target = the existing **Inventory** module (`/edify/inventory/`) — **no form changes** (user rule 2026-09-15). The data must fit the screens as they are:

| EdifyBiz screen | Header table | Line table | Ledger (`stocktrans.module`) |
|---|---|---|---|
| **GRN / MRN** (stock in) `/edify/inventory/grn/` | `stockinout` (`type='GRN'`) | `stockinoutdet` | `GRN`, qty **+** |
| **GRO / MRO** (stock out) `/edify/inventory/mro/` | `stockinout` (`type='MRO'`) | `stockinoutdet` | `MRO`, qty **−** |
| **GTA / MTA** (branch transfer) `/edify/inventory/gta/` | `stockb2b` | `stockb2bdet` | `GTA`, − at from-branch, + at to-branch (branch 0 = in transit) |
| Stock Summary report | — | — | reads `stocktrans` (filter on `createdon`) + `productstocksummary` |

Source tables (offices 2/3/4/6): `trhstkin` + `trdstkin1items` (7,150 docs / 30,583 lines), `trhstkou` + `trdstkou1items` (1,326 / 2,475), `trdstkin5serialno` (22), `trdstkin2posttaxchgs` (65), `trdstkou2posttaxchgs` (6), `trhpstk` + `trdpstk1detail` (5 / **0 lines**), `trhstktrf` (1).

Reference checked in another live EdifyBiz DB (Cona4HO315332): GRNs saved with `module='PO'`, `grntype` = name text, supplier sometimes empty; opening stock posted as `JV`; `productstocksummary.qty` = sum of `stocktrans.qty` for every row.

---

## 1) What the client data is (verified)

**Stock-IN** (`nstkinfrom` / `nstkintype` → `mstfixedselection`):

| From | Type | Docs | Goes to |
|---|---|---|---|
| FROM PARTY | AGAINST ORDER PLACED | 4,759 | GRN against PO |
| FROM PARTY | FRESH | 1,206 | GRN (no PO) |
| FROM PARTY | AGAINST W SALES RETURN | 6 | GRN (sales return) |
| FROM OFFICE | FRESH / AGAINST STOCK OUT | 787 | branch transfer (§4) |
| FROM USER | FRESH | 392 | engineer returns (§5, decision) |

**Stock-OUT** (`nstkoutto` / `nstkoutype`):

| To | Type | Docs | Goes to |
|---|---|---|---|
| TO OFFICE | FRESH | 792 | branch transfer (§4) |
| TO USER | FRESH | 526 | engineer issue (§5, decision) |
| TO PARTY | FRESH / AGAINST ORDER RECEIVED / AGAINST CLAIM | 8 | MRO |

- All documents are approved (`bapproval = 1`). Client balance tables (`msditembalance`, `mststockhistory`) are **empty** — eBizWiz computes stock on the fly: opening + stock-in − stock-out − **sold** − warranty issues.
- 742 office transfers are **pairs**: Stock-OUT at office A ↔ Stock-IN at office B with `trhstkin.nstkouno` = `trhstkou.ncode` (704 of them same total qty). 50 Stock-OUT to office have no Stock-IN; 45 Stock-IN from office have no Stock-OUT.
- A GRN refers to one PO (4,753 docs; 1 doc refers to 22 POs). Party = PO supplier in 4,753 of 4,754.

---

## 2) Stock-IN from party → GRN (`stockinout` `type='GRN'`)

| Client (eBizWiz) | Client DB (`trhstkin`) | Our DB (`stockinout`) | GRN form label |
|---|---|---|---|
| Trn. No. | `vtrnprefix` + `ntrnno` ("SI…") | `number` | GRN Number |
| Trn. Date | `dtrndate` (1 bad date SI2384 = 2202-11-10 → use `addedon` 2022-11-11) | `date` | GRN Date |
| (office) | `nofficeid` → branch 3/4/5/6 | `branchcode` | Branch Name |
| From Party | `nfromparty` → migrated `contact` | `scode` | Supplier |
| Against Order Placed | line `nordpl1` → `trdordpl1items.nordpl` → migrated PO (branch + purorderno) | `module='PO'`, `modulecode` = `pur_order.code` (doc with 22 POs → first PO, rest in remark) | PO/PI + PO/PI Number |
| Stock-In Type | `nstkintype` name (FRESH / AGAINST ORDER PLACED / AGAINST W SALES RETURN) | `grntype` (name text; seed `miscellaneous(GRN/grntype)` so the dropdown shows it) | Type |
| Remarks + Comment + Ref No/Date + Dispatch Mode/Through/Doc No/Date + charges + missing qty | `vremarks`, `vcomment`, `vrefno`, `drefdate`, `ndispatchmode`, `vdespatchthru`, `vdespdocno`, `ddespdocdate`, `trdstkin2posttaxchgs` | `remark` (labelled) | Remarks |
| — | — | `vehicleno`, `taxinvno` (WaterWays-only field), `projectcode`, `status` = NULL | — |

Coverage: Ref No 5,186, Ref Date 5,603, Remarks 3,948, Dispatch Mode 6,414, Through 3,260, Doc No 2,286. `nterms`/`vtermsconditions`/`ncheckset`/`approvedby` are empty.

**Lines → `stockinoutdet`** (`trdstkin1items`):

| Client | Client DB | Our DB (`stockinoutdet`) |
|---|---|---|
| Item | `nitem` → migrated `product` | `productcode` |
| (batch) | — | `prodbatchcode` = product's **Default Batch** (batch is mandatory on the GRN line) |
| Accepted qty | `nquantityaccept` | `qty` (good stock) |
| Rejected qty | `nquantityreject` | `dqty` (damaged) |
| Missing qty | `nquantitymissing` | not stock → remark |
| Rate | `nrate` | `price` |
| Unit | item unit → `product.unitcode` | `unit` |

Qty check: accepted + rejected + missing = ordered `nquantity` on 30,366 of 30,583 lines; rejected on 325 lines, missing on 212. There is no tax / amount on inventory lines — the 65 Stock-IN charge rows (GST, TOTAL …) go to the remark only.

---

## 3) Stock-OUT to party → MRO (`stockinout` `type='MRO'`)

| Client | Client DB (`trhstkou`) | Our DB (`stockinout`) | MRO form label |
|---|---|---|---|
| Trn. No. | `vtrnprefix` + `ntrnno` ("SO…") | `number` | MRO Number |
| Trn. Date | `dtrndate` | `date` | MRO Date |
| (office) | `nofficeid` → branch | `branchcode` | Branch |
| To Party | `ntoparty` → `contact` | `scode` | Contractor |
| Remarks etc. | `vremarks`, `vcomment`, `vrefno`, `drefdate`, dispatch fields, `ncalls`, charges | `remark` | Remarks |

Lines (`trdstkou1items`) → `stockinoutdet`: `nitem` → `productcode`, Default Batch, `nquantity` → `qty`, `ndefquantity` → `dqty` (37 lines), `nrate` → `price`, unit. MRO form has no module/link field → `nordrc1` (6 lines against SO) noted in remark.

---

## 4) Office ↔ office → GTA (`stockb2b` / `stockb2bdet`)

| Client | Our DB (`stockb2b`) |
|---|---|
| Stock-OUT Trn. No. (`trhstkou` SO…) | `number` |
| Stock-OUT date | `date` |
| Stock-OUT office | `frombranchcode` |
| `ntooffice` | `tobranchcode` |
| paired Stock-IN no. + date, remarks, dispatch fields | `remark` |
| — | `intransit` (see decision D2), `type`/`scode`/`transportercode` NULL |

Lines → `stockb2bdet` (product, Default Batch, qty, dqty, unit, price). Ledger: `GTA` − at from-branch, + at to-branch.

---

## 5) Ledger + summary (what the exe must write, since it inserts directly)

- `stocktrans` per line: `date` = document date, `branchcode`, `productcode`, `prodbatchcode`, `qty` (+ GRN / − MRO / ± GTA), `dqty`, `unit`, `module` (`GRN`/`MRO`/`GTA`), `modulecode` = **line** code (`stockinoutdet.code` / `stockb2bdet.code`), `createdon` = document date (the Stock Summary report filters on `createdon`).
- After all inserts: rebuild `productstocksummary` per branch + product + batch = sum(`qty`), sum(`dqty`) from `stocktrans` (same as the app's `InventoryTrigger`).

---

## 6) Not migrated

| Client | Reason |
|---|---|
| Physical Stock Taking (`trhpstk`) | 5 headers, **0 lines** — nothing to post |
| Stock Transfer user → user (`trhstktrf`) | 1 document, no branch change |
| Stock-IN checks / pay terms | 0 / 1 rows |
| Serial numbers (`trdstkin5serialno`) | 22 rows; EdifyBiz serial master (`prodbatchsrno`) needs trading serial + warranty → serials go to the GRN remark |

---

## 7) Decisions (approved 2026-09-15 — "jo best ho, only exe, no form change")

| # | Decision | Exe rule |
|---|---|---|
| D1 | Engineer issue / return | TO USER → **MRO**, FROM USER → **GRN** at the office branch; Supplier/Contractor blank; user name in remark ("Stock-OUT TO USER … User: name") |
| D2 | Office transfers | OUT ↔ IN pair (`nstkouno`) with the **same items** (OUT qty/defective = IN accepted/rejected per item) → one **GTA** (`intransit=0`), number/date = Stock-OUT, remark "Received by Stock-IN …". Pair with different items → OUT as **MRO** + IN as **GRN** (each branch keeps its own real movement). OUT to office with no IN → **GTA in transit** (`intransit=1`, stock at branch 0). IN from office with no OUT → **GRN** |
| D3 | Stock sold | **SAL** rows for migrated Sales Invoice lines (branches 3–6, product type ≠ 's', qty ≠ 0): `qty` −, `dqty` 0, date/createdon = invoice date, `modulecode` = `sal_order_det.code` — same as `SalesInventoryTrigger`. Checked: no invoice is "against unbilled challans" and no Stock-OUT line points to an invoice → no double deduction |
| D4 | Warranty parts on calls (`trdcalls3parts`, 6,858) | with the Call Entry module |
| D5 | Acceptance | after the run: EdifyBiz stock per branch + product = eBizWiz (accepted in − out − sold ± transfers) |

Exe notes (Stock module = menu 8, run after Sales Invoice):
- **Defective qty on MRO/GTA:** the app's `InventoryTrigger` writes MRO/GTA `dqty` positive (it would *add* damaged stock when it leaves). The exe writes `dqty` **negative** at the sending branch (and + at the receiving branch for GTA) so damaged stock is correct. Only 37 Stock-OUT lines have defective qty.
- GRN `grntype` = Stock-IN type name; full `nstkintype` group seeded into `miscellaneous(GRN/grntype)` for the Type dropdown.
- Header/line `createdon` = document date; SI2384 (2202-11-10) uses its entry date.
- `productstocksummary` rebuilt from `stocktrans` at the end (update existing rows, insert missing).
- Can run on the current DB after modules 1–7 (stock tables are empty); re-running needs a fresh restore (no delete logic).

---

## Verified (run 2026-09-15, module 8 on the migrated DB, 0 errors)

- Documents: Stock-IN 7,150 = GRN 6,433 + 717 received inside a GTA; Stock-OUT 1,326 = MRO 559 (526 to user + 8 to party + 25 transfer pairs with different items) + GTA 767 (50 in transit — only 3 of them have lines; 65 Stock-OUT and 95 Stock-IN documents have no lines in eBizWiz either).
- **Closing stock per branch + product = eBizWiz (accepted in − out − sold) on 6,349 of 6,349** (good and defective qty). Compared by product name — 1,733 product names are duplicated in the target, the exe resolves them the same way as the Sales Invoice module.
- GRN from party: PO number 5,971 / 5,971, supplier 5,971 / 5,971, date 5,970 (SI2384 bad source date → entry date).
- Ledger: GRN 29,007 rows (+81,883), MRO 880 (−1,928), GTA 3,190 (net 0; 20 rows at branch 0 in transit), SAL 18,629 (−40,508); 0 rows pointing to a missing line; no row without batch; 3,414 rows without unit (product has no unit in eBizWiz).
- SAL not posted on 64 invoice lines: 58 service items, 6 qty 0 (by design).
- `productstocksummary` 6,367 rows = ledger on 6,367 / 6,367.
- Samples: GRN SI3151 (HO Mumbai, Steelco S.p.A., PO OP2867, AGAINST ORDER PLACED, Door Gasket 6 @ 177, Default Batch); MRO SO794 (TO USER Pankaj Rane, supplier blank, challan text in remark); GTA SO274 Bangalore → HO Mumbai in transit.
