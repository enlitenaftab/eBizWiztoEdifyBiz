# Customer Quotation — Migration Mapping (eBizWiz → EdifyBiz)

Client module **Quotation Entry**. Read as: what the client sees → where it sits in the client DB → which column it goes to in our DB → how it shows in our UI.

Customer Quotation uses the same shared inquiry form as Customer Inquiry, distinguished by `inqcs.cors='CQ'`.

Source tables: `trhquote` (header), `trdquote1items` (line items).
Target tables: `inqcs` (cors='CQ', header), `inqcsdet` (line items).
Scope: offices 2 (HO Mumbai), 3 (Bangalore), 4 (Delhi), 6 (Kolkata). Test offices 1 & 5 excluded.

---

## 1) Quotation header → `inqcs` (cors='CQ')

| Client UI (eBizWiz) | Client DB (`trhquote`) | Our DB (target column) | Our UI (EdifyBiz) |
|---|---|---|---|
| Trn. No. | `vtrnprefix` + `ntrnno` | `inqcs.inqref` | Quotation No. (`.00` / `.10` in the number = original / revision) |
| Trn. Date | `dtrndate` | `inqcs.inqdate` | Quotation Date |
| Party | `nparty` → target `contact` (by name) | `inqcs.cscode` | Party |
| (branch of that customer) | party is per-branch (no field) | `inqcs.csbranch` | Customer Branch (customer's own address) |
| Party Contact | `npartycontact` → `mltcontact.code` | `inqcs.cperson` | Party Contact |
| Quote Type | `nquotetype` → `miscellaneous(Inquiry/Quote Type)` | `inqcs.quotetype` | Quote Type |
| Inquiry No. (against-inquiry) | `ninquiry` → the migrated inquiry (by its ref + branch) | `inqcs.inqlink` | Inquiry No. (links to the source Inquiry) |
| Sales Person | `nsalesman` → target `users` (by name; all source users migrated in Contact) | `inqcs.executive` | Sales Person (real salesperson; fallback Admin) |
| Follow Up Date | `dfollowupdate` | `inqcs.followupdate` (dedicated new column — NOT `validdate`, which is the quotation's own validity) | Follow Up Date |
| Quote Currency | `ncurrency` → `currency` master (by name / short name) | `inqcs.currency` | Currency |
| (exchange rate) | `nexchangerate` | `inqcs.exchangerate` | Exchange Rate |
| Quote Status | `nquotestatus` → `status` master | `inqcs.status` | Status |
| Lost Reason | `nlostreasons` → `miscellaneous(Inquiry/Loss Reason)` | `inqcs.lossreason` | Lost Reason |
| Ref. No. | `vrefno` | `inqcs.enqrefno` | Enquiry/Tender Ref No |
| Ref. Date | `drefdate` | `inqcs.enqrefdate` | Ref Date |
| Inquiry Category | `ninquirycategory` → `miscellaneous(Inquiry/Category)` | `inqcs.Category` | Quote Category |
| Remarks | `vremarks` | `inqcs.remark` | Remarks |

Note: `quotetype`, `enqrefno`, `enqrefdate` are standard EdifyBiz columns present for other companies; enabled for saksham (form + view) so this data is structured, not dumped into remark.

---

## 2) Line items → `inqcsdet`

| Client UI | Client DB (`trdquote1items`) | Our DB (`inqcsdet`) | Our UI |
|---|---|---|---|
| Product | `nitem` → target `product` (by name) | `pcode` | product row |
| Quantity | `nquantity` | `quantity` | quantity |
| Rate | `nrate` | `price` | price |
| Discount % | `ndiscountperc` | `discountpercent` | discount % |
| Line Currency | `nitemcurrency` → `currency` master | `currency` | currency |

---

## 3) Fields folded into Remarks (no structured target)

Appended (labelled) into `inqcs.remark`, nothing lost:

| Client UI (eBizWiz) | Client DB (`trhquote`) |
|---|---|
| Comment | `vcomment` |
| Validity (free text, e.g. "90 Days") | `vvalidity` |

---

## 4) Not migrated / auto (no target, or filled by the system)

| Client UI | Client DB | Reason |
|---|---|---|
| Terms & Cond. | `nterms` / `vtermsconditions` | source is empty/sparse; no data to carry |
| Check List | `ncheckset` | process master, not migrated |
| Signatory | `nsignatory` | no matching field in `inqcs` |
| Trn. Total | — | derived from the line items |
| Quote For Item Groups / Show Items Of Selected Currency / Print with Price / Print with Terms / Select Letterhead | UI flags | print/display options, not data |

_Verified 2026-09-15 (full data): CQ → Inquiry link 110,379 of 110,379, 0 wrong (12 where CQ party ≠ inquiry party are the same in source)._

## Tax, charges & discount (added 2026-09-15)

eBizWiz quotations carry GST as **post-tax charges** (`trdquote3posttaxchgs` + `mstprepostchgs`): 31,833 of 110,493 quotations (39,451 GST charge rows), same structure and formula as the Sales Invoice (07 §5). Formula check vs `ntotalamount`: 31,729 of 31,833 GST quotations (the rest have no source total), 109,703 of 110,493 overall. 545 quotations also use the line Tax Set.

Same rules as the Sales Order (05, "Tax, charges & discount"), because CQ and SO share the saksham view (`inquiry.js` `handleProductViewFill`: no tax column, total = qty × (price − discount) + `inq_adjust`):

| eBizWiz | Rule | Our DB |
|---|---|---|
| `nmasterrate`, `ndiscountperc`, `nrate` (net) | `nrate` = master × (1 − %) on 255,861 of 255,864 discounted lines. Discounted → price = master, discount = master − `nrate` (per unit), % kept; else price = `nrate` | `inqcsdet.price`, `discount`, `discountpercent` |
| GST % | main GST charge (IGST x / CGST x + SGST y / GST x); none → line Tax Set % | `inqcsdet.prodtax` |
| Every charge incl. GST lines | amount, or item total × % (07 §5 formula), in entry order | `inq_adjust` |
| Line Tax Set amounts / unmapped product lines | "Tax on items (eBizWiz)" / "Other items (eBizWiz)" | `inq_adjust` |

Result: quotation Grand Total = eBizWiz total, GST lines as the client printed them. No ALTER (`inq_adjust.adjustname` is varchar(max)).

_Verified 2026-09-15 (run before the % fix): all 110,493 quotations — line value = eBizWiz qty × `nrate` (0 wrong); total vs eBizWiz 109,405 of 109,567 with a source total; of the rest 10 have no source lines, 11 a source header that doesn't match its own lines, 142 were the "% on running base" rule → fixed in exe (% on item total)._

_Run 2 verified 2026-09-15 (after "% on item total"): total differences down from 163 to 37 — 10 quotations with no source lines, 5 source header ≠ its own lines, 22 source totals that don't follow the client's own charges (e.g. QT25953.20 items 1,42,000 + 4,260 = 1,46,260 but source total 1,42,426). Tax % "NIL"/>28 %/no-amount fix applied in exe._
