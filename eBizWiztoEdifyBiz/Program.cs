using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using static eBizWiztoEdifyBiz.Shared;

// ============================================================================
//  eBizWiz -> EdifyBiz  Data Migration  (single file, .NET Framework 4.7.2)
//  All modules live here, block-wise (one static class per module).
//  SAFETY: SELECT the source, INSERT into the target. No destructive operations.
// ============================================================================
namespace eBizWiztoEdifyBiz
{
    // ==================================================================
    //  SHARED : config, cross-module state, and small ADO/value helpers
    // ==================================================================
    static class Shared
    {
        // Same server / uid / pwd for both databases.
        public const string srccnstr = @"Server=ENLITEN\SQLEXPRESS05;Database=saksham70v1_1;Uid=sa;Pwd=password@1;";
        public const string tgtcnstr = @"Server=ENLITEN\SQLEXPRESS05;Database=SakshamRMtP15329;Uid=sa;Pwd=password@1;";

        // User stamped as creator/updater on every migrated row (1 = Admin).
        public const int MigrationUser = 1;
        // Saksham company row in [company] (code 1). Offices become [companyaddress] branches.
        public const int CompanyCode = 1;
        // Source offices to migrate: 2=HO Mumbai, 3=Bangalore, 4=Delhi, 6=Kolkata. Test offices 1 & 5 excluded.
        public static readonly int[] Offices = { 2, 3, 4, 6 };
        public static string OfficeIn { get { return string.Join(",", Offices); } }

        // Cross-module state (filled during a "Run ALL" pass).
        // source party NKey -> target contact.code (filled by Contact, used by Product/Inquiry/...).
        public static readonly Dictionary<string, int> PartyToContact = new Dictionary<string, int>();
        // source mstusers NKey -> target users.code (by name). Resolves each salesperson to the real user.
        public static readonly Dictionary<string, int> UserByCode = new Dictionary<string, int>(StringComparer.Ordinal);

        // ---- connections ----
        public static SqlConnection OpenSrc() { SqlConnection c = new SqlConnection(srccnstr); c.Open(); return c; }
        public static SqlConnection OpenTgt() { SqlConnection c = new SqlConnection(tgtcnstr); c.Open(); return c; }

        // ---- GetRS: run a query and return a DataSet ----
        public static DataSet GetSrc(string sql) { return GetRS(sql, srccnstr); }
        public static DataSet GetTgt(string sql) { return GetRS(sql, tgtcnstr); }
        public static DataSet GetRS(string strSQL, string cnstr)
        {
            using (SqlConnection cn = new SqlConnection(cnstr))
            using (SqlCommand cmd = new SqlCommand(strSQL, cn))
            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
            {
                cmd.CommandTimeout = 0;
                DataSet ds = new DataSet();
                da.Fill(ds);
                return ds;
            }
        }

        // ---- source lookup loaders (key -> name) ----
        public static Dictionary<int, string> SrcLookupInt(string sql)
        {
            Dictionary<int, string> d = new Dictionary<int, string>();
            DataSet ds = GetSrc(sql);
            foreach (DataRow r in ds.Tables[0].Rows) { int k = Int(r[0]); if (!d.ContainsKey(k)) d[k] = Str(r[1]); }
            return d;
        }
        public static Dictionary<string, string> SrcLookupNKey(string sql)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            DataSet ds = GetSrc(sql);
            foreach (DataRow r in ds.Tables[0].Rows) { string k = NKey(r[0]); if (k.Length > 0 && !d.ContainsKey(k)) d[k] = Str(r[1]); }
            return d;
        }

        // Build UserByCode once (idempotent): source mstusers NKey -> target users.code, matched by name.
        public static void EnsureUserByCode()
        {
            if (UserByCode.Count > 0) return;
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM users").Tables[0].Rows)
            { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }
            foreach (DataRow r in GetSrc("SELECT ncode, vname FROM mstusers WITH (NOLOCK)").Tables[0].Rows)
            {
                string k = NKey(r["ncode"]); string nm = Str(r["vname"]).Trim();
                if (nm.Length > 150) nm = nm.Substring(0, 150);
                int code;
                if (k.Length > 0 && nm.Length > 0 && byName.TryGetValue(nm, out code) && !UserByCode.ContainsKey(k))
                    UserByCode[k] = code;
            }
        }

        // ---- value helpers ----
        public static string Str(object v) { return v == null || v == DBNull.Value ? "" : Convert.ToString(v, CultureInfo.InvariantCulture).Trim(); }
        public static decimal Dec(object v) { return v == null || v == DBNull.Value ? 0m : Convert.ToDecimal(v, CultureInfo.InvariantCulture); }
        public static int Int(object v)
        {
            if (v == null || v == DBNull.Value) return 0;
            int r; return int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out r) ? r : 0;
        }
        public static bool Bool(object v) { return v != null && v != DBNull.Value && Convert.ToBoolean(v); }
        // Stable, scale-independent string key for a decimal source ncode (drops trailing zeros).
        public static string NKey(object v) { return v == null || v == DBNull.Value ? "" : Dec(v).ToString("0.############", CultureInfo.InvariantCulture); }
        // DBNull-safe parameter value.
        public static object P(object v) { return v ?? DBNull.Value; }
        // Empty string -> NULL (so we don't write "" where the app writes NULL).
        public static object PS(string v) { return string.IsNullOrWhiteSpace(v) ? (object)DBNull.Value : v.Trim(); }
        public static DateTime? Date(object v) { return v == null || v == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(v); }
        public static string Cap(string s, int n) { return string.IsNullOrEmpty(s) ? s : (s.Length > n ? s.Substring(0, n) : s); }

        public static string LookInt(Dictionary<int, string> d, int k) { string v; return d != null && d.TryGetValue(k, out v) ? v : ""; }
        public static string LookStr(Dictionary<string, string> d, string k) { string v; return d != null && d.TryGetValue(k, out v) ? v : ""; }

        // product.code -> its 'Default Batch' prodbatch.code (created by the Product module). Used for invoice / PO line prodbatchcode. - Aftab Alam
        public static Dictionary<int, int> DefaultBatchByProduct()
        {
            Dictionary<int, int> map = new Dictionary<int, int>();
            foreach (DataRow r in GetTgt(@"SELECT pbr.prodcode, MIN(pb.code) AS batchcode FROM prodbatch pb
                                           JOIN prodbrandmodel pbm ON pb.pbmcode = pbm.code
                                           JOIN prodbrandrelation pbr ON pbm.pbcode = pbr.code
                                           WHERE pb.batchno = 'Default Batch' GROUP BY pbr.prodcode").Tables[0].Rows)
                map[Int(r["prodcode"])] = Int(r["batchcode"]);
            return map;
        }

        // ---- GST setup (used by Sales Invoice tax) - Aftab Alam ----
        // EdifyBiz decides CGST+SGST vs IGST by comparing company-branch city->state->stategstcode with the customer
        // Bill To branch city->state->stategstcode. Target state master has no GST codes and the branches no city.
        // Source proof: ALL saksham offices billed on the Maharashtra GSTIN (Maharashtra customer -> CGST+SGST 512/512,
        // other state -> IGST 1031/1046, Bangalore office charged IGST to Karnataka customers) -> branch GST city = HO (office 2) city.
        public const int GstHomeOffice = 2;
        public static int GstHomeStateCode;             // state.code of the GST registration state
        public static string GstHomeGstCode = "";       // its stategstcode ("27")
        static bool _gstSetupDone;

        // Official GST state codes, keyed by normalised state name (letters only, "&"/"AND" dropped). Includes the
        // misspelt / city-as-state rows present in the target master (Telngana, Uttaranchal, CHENNAI, NOIDA).
        static readonly Dictionary<string, string> GstStateCodes = new Dictionary<string, string>
        {
            {"JAMMUKASHMIR","01"},{"HIMACHALPRADESH","02"},{"PUNJAB","03"},{"CHANDIGARH","04"},{"UTTARAKHAND","05"},{"UTTARANCHAL","05"},
            {"HARYANA","06"},{"DELHI","07"},{"RAJASTHAN","08"},{"UTTARPRADESH","09"},{"NOIDA","09"},{"BIHAR","10"},{"SIKKIM","11"},
            {"ARUNACHALPRADESH","12"},{"NAGALAND","13"},{"MANIPUR","14"},{"MIZORAM","15"},{"TRIPURA","16"},{"MEGHALAYA","17"},
            {"ASSAM","18"},{"WESTBENGAL","19"},{"JHARKHAND","20"},{"ODISHA","21"},{"ORISSA","21"},{"CHHATTISGARH","22"},
            {"MADHYAPRADESH","23"},{"GUJARAT","24"},{"DAMANDIU","26"},{"DADRANAGARHAVELI","26"},{"MAHARASHTRA","27"},
            {"KARNATAKA","29"},{"GOA","30"},{"LAKSHADWEEP","31"},{"LAKSHDWEEP","31"},{"KERALA","32"},{"TAMILNADU","33"},{"CHENNAI","33"},
            {"PUDUCHERRY","34"},{"PONDICHERRY","34"},{"ANDAMANNICOBAR","35"},{"TELANGANA","36"},{"TELNGANA","36"},
            {"ANDHRAPRADESH","37"},{"LADAKH","38"}
        };

        static string NormState(string n)
        {
            string s = " " + (n ?? "").ToUpperInvariant().Replace("&", " ") + " ";
            s = s.Replace(" AND ", " ");
            return new string(s.Where(char.IsLetter).ToArray());
        }

        public static void EnsureGstSetup(SqlConnection tgt)
        {
            if (_gstSetupDone) return;
            int filled = 0;
            List<string> unmapped = new List<string>();
            foreach (DataRow r in GetTgt("SELECT code, name, countrycode, ISNULL(stategstcode,'') AS g FROM state").Tables[0].Rows)
            {
                if (Str(r["g"]).Length > 0) continue;
                string g;
                if (!GstStateCodes.TryGetValue(NormState(Str(r["name"])), out g)) { if (Int(r["countrycode"]) == 101) unmapped.Add(Str(r["name"])); continue; }
                using (SqlCommand u = new SqlCommand("UPDATE state SET stategstcode=@g WHERE code=@c AND ISNULL(stategstcode,'')=''", tgt))
                { u.Parameters.AddWithValue("@g", g); u.Parameters.AddWithValue("@c", Int(r["code"])); filled += u.ExecuteNonQuery(); }
            }
            Console.WriteLine("  GST: state GST codes filled = " + filled + (unmapped.Count > 0 ? "  (India, no GST code: " + string.Join(", ", unmapped) + ")" : ""));

            // Home GST city = source office 2 city, matched by name to a target city whose state now has a GST code.
            string homeCity = "";
            foreach (DataRow r in GetSrc("SELECT c.vname FROM mstoffice o WITH (NOLOCK) JOIN mstcity c WITH (NOLOCK) ON c.ncode = o.ncity WHERE o.ncode = " + GstHomeOffice).Tables[0].Rows) homeCity = Str(r[0]);
            int cityCode = 0;
            using (SqlCommand q = new SqlCommand("SELECT TOP 1 c.code, s.code, s.stategstcode FROM city c JOIN state s ON s.code = c.statecode WHERE c.name = @n AND ISNULL(s.stategstcode,'') <> '' ORDER BY c.code", tgt))
            {
                q.Parameters.AddWithValue("@n", homeCity);
                using (SqlDataReader rd = q.ExecuteReader())
                    if (rd.Read()) { cityCode = Int(rd[0]); GstHomeStateCode = Int(rd[1]); GstHomeGstCode = Str(rd[2]); }
            }
            if (cityCode == 0) { Console.WriteLine("  [WARN] GST: home city '" + homeCity + "' not found in target city master - branch GST state left empty"); _gstSetupDone = true; return; }

            // Branches (companyaddress place = source office name) with no city -> home GST city. Never overwrites a set city.
            int branches = 0;
            foreach (DataRow r in GetSrc("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK) WHERE ncode IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string place = Cap(Str(r["vcompanyname"]), 100);
                using (SqlCommand u = new SqlCommand("UPDATE companyaddress SET citycode=@city, statecode=ISNULL(statecode,@st) WHERE ccode=@cc AND place=@pl AND citycode IS NULL", tgt))
                {
                    u.Parameters.AddWithValue("@city", cityCode); u.Parameters.AddWithValue("@st", GstHomeStateCode);
                    u.Parameters.AddWithValue("@cc", CompanyCode); u.Parameters.AddWithValue("@pl", place);
                    branches += u.ExecuteNonQuery();
                }
            }
            Console.WriteLine("  GST: home state = " + GstHomeGstCode + " (city '" + homeCity + "'), branches given GST city = " + branches);
            _gstSetupDone = true;
        }

        // Tax master code by name (CGST/SGST/IGST); created if missing, same as the app's GetTaxCode(name, True).
        public static int TaxCode(SqlConnection tgt, string name)
        {
            using (SqlCommand q = new SqlCommand("SELECT TOP 1 code FROM tax WHERE name=@n ORDER BY code", tgt))
            {
                q.Parameters.AddWithValue("@n", name);
                object o = q.ExecuteScalar();
                if (o != null && o != DBNull.Value) return Convert.ToInt32(o);
            }
            using (SqlCommand ins = new SqlCommand("INSERT INTO tax (name, [percent], makedefault) OUTPUT INSERTED.code VALUES (@n, 0, 0)", tgt))
            { ins.Parameters.AddWithValue("@n", name); return Convert.ToInt32(ins.ExecuteScalar()); }
        }

        // ---- eBizWiz post-tax charges (GST + freight/discount/...) for Quotation / Sales Order - Aftab Alam ----
        // Source: <table> (trdquote3posttaxchgs / trdordrc4posttaxchgs) + mstprepostchgs, in entry order.
        // object[] = { label, mode (AMOUNT/PERCENTAGE), percent, amount, applyOnItemTotalOnly }
        public static Dictionary<string, List<object[]>> LoadPostTaxCharges(string table, string fk)
        {
            Dictionary<string, List<object[]>> map = new Dictionary<string, List<object[]>>();
            int n = 0;
            foreach (DataRow r in GetSrc("SELECT c.nofficeid, c." + fk + " AS doc, p.vname, c.venteredinamtorper, c.ntaxpercentage, c.namount, c.bapplyonitemtotalonly " +
                                         "FROM " + table + " c WITH (NOLOCK) JOIN mstprepostchgs p WITH (NOLOCK) ON p.ncode = c.nmstposttaxchgs " +
                                         "WHERE c.nofficeid IN (" + OfficeIn + ") ORDER BY c.nofficeid, c." + fk + ", c.ncode").Tables[0].Rows)
            {
                string key = Int(r["nofficeid"]) + "|" + Int(r["doc"]);
                List<object[]> list;
                if (!map.TryGetValue(key, out list)) { list = new List<object[]>(); map[key] = list; }
                list.Add(new object[] { Str(r["vname"]).Trim(), Str(r["venteredinamtorper"]).ToUpperInvariant(), Dec(r["ntaxpercentage"]), Dec(r["namount"]), Bool(r["bapplyonitemtotalonly"]) });
                n++;
            }
            Console.WriteLine("  Post-tax charges loaded: " + n + " across " + map.Count + " documents (" + table + ")");
            return map;
        }

        // eBizWiz charge formula: a PERCENTAGE charge (discount or GST) is always on the ITEM TOTAL (qty x nrate, all lines); AMOUNT
        // charges are taken as entered. Verified vs ntotalamount on docs having % charges: invoices 1,603/1,604, SO 3,373/3,374,
        // quotations 22,223/22,236 (the earlier "running base" rule matched fewer). Returns every non-zero charge as { label, percentShown, amount } in entry order, and the
        // product GST % (IGST x | CGST x + SGST y | plain GST x; largest amount of a kind; "GST ... on <charges>" excluded).
        public static List<object[]> ComputeCharges(decimal itemsAll, List<object[]> charges, out decimal gstRate)
        {
            List<object[]> rows = new List<object[]>();
            decimal gstTotal = 0m;
            decimal amtI = -1, rI = 0, amtC = -1, rC = 0, amtS = -1, rS = 0, amtG = -1, rG = 0;
            if (charges != null)
                foreach (object[] ch in charges)
                {
                    string label = (string)ch[0]; bool pctMode = (string)ch[1] == "PERCENTAGE"; decimal pct = (decimal)ch[2];
                    decimal amt = pctMode ? Math.Round(itemsAll * pct / 100m, 2) : (decimal)ch[3];
                    bool isGst = label.IndexOf("GST", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (amt != 0) rows.Add(new object[] { label, pctMode ? pct : 0m, amt });
                    if (!isGst) continue;
                    if (label.IndexOf(" on ", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    gstTotal += amt;
                    decimal rate = pctMode && pct > 0 && pct <= 28 ? pct : GstRateIn(label);
                    if (rate <= 0) continue;
                    if (label.IndexOf("IGST", StringComparison.OrdinalIgnoreCase) >= 0) { if (amt > amtI) { amtI = amt; rI = rate; } }
                    else if (label.IndexOf("CGST", StringComparison.OrdinalIgnoreCase) >= 0) { if (amt > amtC) { amtC = amt; rC = rate; } }
                    else if (label.IndexOf("SGST", StringComparison.OrdinalIgnoreCase) >= 0) { if (amt > amtS) { amtS = amt; rS = rate; } }
                    else { if (amt > amtG) { amtG = amt; rG = rate; } }
                }
            gstRate = gstTotal == 0 ? 0m : (rI > 0 ? rI : (rC + rS > 0 ? rC + rS : rG));   // GST label without any amount = no GST
            return rows;
        }

        // Quotation / Sales Order lines + charges -> inqcsdet tax % and inq_adjust rows. The saksham CQ/SO view (inquiry.js
        // handleProductViewFill) shows no tax and totals = sum(qty x (price - discount)) + inq_adjust, so the eBizWiz GST/charge
        // lines go to inq_adjust exactly as the client printed them; inqcsdet.prodtax keeps the GST % per line. - Aftab Alam
        public static int InsertInqAdjustments(SqlConnection tgt, SqlTransaction tx, int inqcode, List<object[]> rows, object createdon, object updatedon)
        {
            int n = 0;
            foreach (object[] a in rows)
            {
                using (SqlCommand q = new SqlCommand(@"INSERT INTO inq_adjust (inqcode, adjustname, adjustpercent, adjustamount, createdby, updatedby, createdon, updatedon)
                                                     VALUES (@i, @n, @p, @a, @cb, @cb, @con, @uon)", tgt, tx))
                {
                    q.Parameters.AddWithValue("@i", inqcode);
                    q.Parameters.AddWithValue("@n", (string)a[0]);
                    q.Parameters.AddWithValue("@p", a[1]);
                    q.Parameters.AddWithValue("@a", a[2]);
                    q.Parameters.AddWithValue("@cb", MigrationUser);
                    q.Parameters.AddWithValue("@con", createdon);
                    q.Parameters.AddWithValue("@uon", updatedon);
                    q.ExecuteNonQuery();
                }
                n++;
            }
            return n;
        }

        // eBizWiz "B / E of Period" dropdown (Contract Entry / AMC Quotation): stored B / E, shown BEGINNING OF PERIOD / END OF PERIOD.
        public static string BeginOrEnd(string v)
        {
            v = (v ?? "").Trim().ToUpperInvariant();
            return v == "B" ? "Beginning of Period" : v == "E" ? "End of Period" : v;
        }

        // GST rate from a charge label: "NIL" labels ("GST : NIL for 100% EOU against Form A") and anything above 28% are not a rate -> 0.
        public static decimal GstRateIn(string label)
        {
            if ((label ?? "").IndexOf("NIL", StringComparison.OrdinalIgnoreCase) >= 0) return 0m;
            decimal r = PercentIn(label);
            return r > 28m ? 0m : r;
        }

        // First "<number>%" in a label, e.g. "Add : CGST @9%" -> 9, "18% IGST" -> 18. 0 when none.
        public static decimal PercentIn(string label)
        {
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(label ?? "", @"(\d+(?:\.\d+)?)\s*%");
            decimal d;
            return m.Success && decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ? d : 0m;
        }
    }

    // ==================================================================
    //  PROGRAM : menu (pick a module or run ALL in order)
    // ==================================================================
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("############################################################");
            Console.WriteLine("#   eBizWiz -> EdifyBiz  Data Migration                    #");
            Console.WriteLine("#   Source : saksham70v1_1                                 #");
            Console.WriteLine("#   Target : SakshamRMtP15329                              #");
            Console.WriteLine("############################################################");
            Console.WriteLine();

            // Registration order = execution order for "ALL".
            string[] names = { "Contact", "Product", "Customer Inquiry", "Customer Quotation", "Sales Order", "Purchase Order", "Sales Invoice", "Stock", "AMC Quotation", "AMC Contract" };

            string choice = (args != null && args.Length > 0) ? string.Join(" ", args).Trim() : null;
            if (choice == null)
            {
                Console.WriteLine("Available modules:");
                for (int i = 0; i < names.Length; i++) Console.WriteLine("  " + (i + 1) + ". " + names[i]);
                Console.WriteLine("  A. Run ALL (in order)");
                Console.WriteLine();
                Console.Write("Select module (number / name / A): ");
                choice = Console.ReadLine();
                if (choice != null) choice = choice.Trim();
            }

            List<int> toRun = Resolve(choice, names);
            if (toRun.Count == 0) { Console.WriteLine("Nothing selected. Exiting."); return; }

            foreach (int idx in toRun)
            {
                Console.WriteLine();
                Console.WriteLine(">>> Running module: " + names[idx]);
                try { RunModule(idx); }
                catch (Exception ex)
                {
                    Console.WriteLine("!!! Module '" + names[idx] + "' failed: " + ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Done. Press any key to exit.");
            Console.ResetColor();
            if (!Console.IsInputRedirected) Console.ReadKey();
        }

        static void RunModule(int idx)
        {
            switch (idx)
            {
                case 0: Contact.Run(); break;
                case 1: Product.Run(); break;
                case 2: CustomerInquiry.Run(); break;
                case 3: CustomerQuotation.Run(); break;
                case 4: SalesOrder.Run(); break;
                case 5: PurchaseOrder.Run(); break;
                case 6: SalesInvoice.Run(); break;
                case 7: Stock.Run(); break;
                case 8: AmcQuotation.Run(); break;
                case 9: AmcContract.Run(); break;
            }
        }

        static List<int> Resolve(string choice, string[] names)
        {
            List<int> list = new List<int>();
            if (string.IsNullOrWhiteSpace(choice)) return list;
            if (choice.Equals("A", StringComparison.OrdinalIgnoreCase) || choice.Equals("all", StringComparison.OrdinalIgnoreCase))
            { for (int i = 0; i < names.Length; i++) list.Add(i); return list; }
            int n;
            if (int.TryParse(choice, out n) && n >= 1 && n <= names.Length) { list.Add(n - 1); return list; }
            for (int i = 0; i < names.Length; i++) if (names[i].Equals(choice, StringComparison.OrdinalIgnoreCase)) { list.Add(i); return list; }
            return list;
        }
    }

    // ==================================================================
    //  CONTACT (Party Master) : mstparty/msdparty/mstusers -> contact/mltaddress/mltcontact
    //  Also seeds country/state/city, designations, communication, party type/profile,
    //  migrates ALL source users, and creates office branches (companyaddress).
    //  Fills Shared.PartyToContact (source party NKey -> target contact.code).
    // ==================================================================
    static class Contact
    {
        static Dictionary<int, string> _titles, _partyTypes, _partyProfiles, _officeName, _srcCityName, _designations;
        static Dictionary<string, string> _routes, _users;
        static readonly Dictionary<string, int> _userByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        static Dictionary<string, int> _tgtCity;
        static Dictionary<int, int> _branchByOffice;
        static Dictionary<string, List<Person>> _persons;
        static readonly HashSet<string> _unmatchedCities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static int _cParties, _cPersons, _cAddresses, _cSkipped, _cErrors, _newCountries, _newStates, _newCities;

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: CONTACT");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            PartyToContact.Clear();
            LoadSourceMasters();

            using (SqlConnection tgt = OpenTgt())
            {
                SeedCitiesAndStates(tgt);
                SeedDesignations(tgt);
                SeedCommunication(tgt);
                SeedPartyTypes(tgt);
                SeedPartyProfiles(tgt);
                EnsureUsers(tgt);
                EnsureBranches(tgt);
                LoadPersons();

                Console.WriteLine("Source masters: titles=" + _titles.Count + ", types=" + _partyTypes.Count +
                                  ", profiles=" + _partyProfiles.Count + ", routes=" + _routes.Count +
                                  ", srcCities=" + _srcCityName.Count + ", users=" + _users.Count);
                Console.WriteLine("Target cities: " + _tgtCity.Count + " (Seeded: +" + _newCountries + " countries, +" + _newStates + " states, +" + _newCities + " cities)");
                Console.WriteLine("Branches: " + _branchByOffice.Count + " | Persons: " + _persons.Count + " parties");
                Console.WriteLine("-----------------------------------------------------");

                string sql = @"
                    SELECT p.ncode, p.vname, p.ntitle, p.npartytype, p.npartyprofile, p.ncity, p.nroute,
                           p.nusersales, p.nuserservice, p.bactive, p.nofficeid,
                           p.vwebsite, p.vinstaddress, p.vinstpostalcode, p.vinsttel1, p.vinsttel2, p.vinstfax, p.vinstemail,
                           p.vbilladdress, p.vbillpostalcode, p.vbilltel1, p.vbilltel2, p.vbillfax, p.vbillemail,
                           p.vtax1, p.vtax2, p.vtax3, p.vremindermessage, p.vremarks,
                           p.addedon, p.editedon
                    FROM mstparty p WITH (NOLOCK)
                    WHERE p.nofficeid IN (" + OfficeIn + @")
                    ORDER BY p.ncode";

                using (SqlConnection src = OpenSrc())
                using (SqlCommand rc = new SqlCommand(sql, src))
                {
                    rc.CommandTimeout = 0;
                    using (SqlDataReader dr = rc.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            try { MigrateParty(tgt, dr); }
                            catch (Exception ex) { _cErrors++; Console.WriteLine("  [ERROR] party " + NKey(dr["ncode"]) + ": " + ex.Message); }
                        }
                    }
                }

                WriteUnmatchedCityLog();
            }

            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine("  Contacts migrated : " + _cParties);
            Console.WriteLine("  Persons migrated  : " + _cPersons + "  (Customer + Sales + Service)");
            Console.WriteLine("  Addresses created : " + _cAddresses);
            Console.WriteLine("  Cities seeded     : " + _newCities + " (+" + _newCountries + " countries, +" + _newStates + " states)");
            Console.WriteLine("  Skipped (no name) : " + _cSkipped);
            Console.WriteLine("  Errors            : " + _cErrors);
            Console.WriteLine("-----------------------------------------------------");
        }

        static void MigrateParty(SqlConnection tgt, SqlDataReader dr)
        {
            string nkey = NKey(dr["ncode"]);
            string title = LookInt(_titles, Int(dr["ntitle"]));
            string name = Trim(Join(title, Str(dr["vname"])), 150);
            if (string.IsNullOrWhiteSpace(name)) { _cSkipped++; return; }

            string ctype = LookInt(_partyTypes, Int(dr["npartytype"]));
            string companyGroup = LookInt(_partyProfiles, Int(dr["npartyprofile"]));
            int isDeleted = Bool(dr["bactive"]) ? 0 : 1;

            string tax1 = Str(dr["vtax1"]), tax2 = Str(dr["vtax2"]), tax3 = Str(dr["vtax3"]);
            string remark = BuildRemark(Str(dr["vremarks"]), tax1, tax2, tax3);
            string reminder = Str(dr["vremindermessage"]);

            int office = Int(dr["nofficeid"]);
            object branch;
            int b;
            branch = _branchByOffice.TryGetValue(office, out b) ? (object)b.ToString(CultureInfo.InvariantCulture) : DBNull.Value;

            object createdon = P(dr["addedon"]);
            object updatedon = dr["editedon"] != DBNull.Value ? dr["editedon"] : P(dr["addedon"]);

            int ncity = Int(dr["ncity"]);
            int? cityCode = ResolveCity(ncity);
            string area = LookStr(_routes, NKey(dr["nroute"]));
            string place = PlaceFor(area, ncity, office);

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                int addrN = 0, persN = 0;
                try
                {
                    int ccode;
                    // Parent-level: crmagent = Sales Person, fieldagent = Service Person (per senior).
                    string salesName = LookStr(_users, NKey(dr["nusersales"]));
                    int su;
                    object crmAgent = (Dec(dr["nusersales"]) > 0 && _userByName.TryGetValue(salesName, out su)) ? (object)su : DBNull.Value;
                    string serviceName = LookStr(_users, NKey(dr["nuserservice"]));
                    int sv;
                    object fieldAgent = (Dec(dr["nuserservice"]) > 0 && _userByName.TryGetValue(serviceName, out sv)) ? (object)sv : DBNull.Value;

                    string insC = @"
                        INSERT INTO contact
                            (name, ctype, companygroup, remark, custom1, pan, gst_no, cst, isdeleted, companybranch,
                             crmagent, fieldagent, owner, ownershipchangedby, ownershipchangedon, createdby, updatedby, createdon, updatedon)
                        OUTPUT INSERTED.code
                        VALUES (@name, @ctype, @cg, @remark, @custom1, @pan, @gst_no, @cst, @isdel, @branch,
                                @crmagent, @fieldagent, @cb, @cb, @uon, @cb, @cb, @con, @uon)";
                    using (SqlCommand c = new SqlCommand(insC, tgt, tx))
                    {
                        c.Parameters.AddWithValue("@name", name);
                        c.Parameters.AddWithValue("@ctype", PS(ctype));
                        c.Parameters.AddWithValue("@cg", PS(companyGroup));
                        c.Parameters.AddWithValue("@remark", PS(remark));
                        c.Parameters.AddWithValue("@custom1", PS(reminder));
                        // Source tax fields are freeform labelled text; columns ALTERed to nvarchar(50). Full text also in remark.
                        c.Parameters.AddWithValue("@pan", PS(Cap(tax1, 50)));
                        c.Parameters.AddWithValue("@gst_no", PS(Cap(tax2, 50)));
                        c.Parameters.AddWithValue("@cst", PS(Cap(tax3, 50)));
                        c.Parameters.AddWithValue("@isdel", isDeleted);
                        c.Parameters.AddWithValue("@branch", branch);
                        c.Parameters.AddWithValue("@crmagent", crmAgent);
                        c.Parameters.AddWithValue("@fieldagent", fieldAgent);
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        c.Parameters.AddWithValue("@con", createdon);
                        c.Parameters.AddWithValue("@uon", updatedon);
                        ccode = Convert.ToInt32(c.ExecuteScalar());
                    }

                    string instAddr = Str(dr["vinstaddress"]);
                    string billAddr = Str(dr["vbilladdress"]);
                    bool separateBilling =
                        !string.IsNullOrWhiteSpace(billAddr) &&
                        !billAddr.Equals(instAddr, StringComparison.OrdinalIgnoreCase) &&
                        billAddr.IndexOf("same as above", StringComparison.OrdinalIgnoreCase) < 0;

                    // Capture the default (install) branch code so each person inherits the parent contact's branch. - Aftab Alam
                    int defaultBranch = 0;
                    if (!string.IsNullOrWhiteSpace(instAddr) || !string.IsNullOrWhiteSpace(area) ||
                        !string.IsNullOrWhiteSpace(Str(dr["vinsttel1"])) || cityCode.HasValue)
                    {
                        defaultBranch = InsertAddress(tgt, tx, ccode, place, instAddr, area, cityCode,
                            Str(dr["vinstpostalcode"]), Str(dr["vinsttel1"]), Str(dr["vinsttel2"]),
                            Str(dr["vinstemail"]), Str(dr["vinstfax"]), Str(dr["vwebsite"]),
                            1, 0, 1, createdon, updatedon);
                        addrN++;
                    }

                    if (separateBilling)
                    {
                        int billBranch = InsertAddress(tgt, tx, ccode, place, billAddr, "", cityCode,
                            Str(dr["vbillpostalcode"]), Str(dr["vbilltel1"]), Str(dr["vbilltel2"]),
                            Str(dr["vbillemail"]), Str(dr["vbillfax"]), "",
                            0, 1, 0, createdon, updatedon);
                        if (defaultBranch == 0) defaultBranch = billBranch;   // fallback if no install address
                        addrN++;
                    }

                    List<Person> plist;
                    if (_persons.TryGetValue(nkey, out plist))
                    {
                        bool first = true;
                        foreach (Person p in plist)
                        {
                            string persName = Trim(Join(LookInt(_titles, p.TitleId), p.Name), 100);
                            if (string.IsNullOrWhiteSpace(persName)) continue;
                            string other = p.Remarks;
                            if (!string.IsNullOrWhiteSpace(p.SpouseName))
                                other = string.IsNullOrWhiteSpace(other) ? "Spouse: " + p.SpouseName : other + " | Spouse: " + p.SpouseName;

                            // Phone: keep source semantics — vmobile -> mobile1, vtel1/vtel2 -> landlines
                            // (vtel1 usually holds a LANDLINE, so do NOT promote it into mobile). - Aftab Alam
                            InsertContactRow(tgt, tx, ccode, persName, p.Designation, p.Mobile, p.Tel1, p.Tel2,
                                p.Email, other, p.Dob, p.Anniversary, p.SpouseDob, p.Child1Dob, p.Child2Dob,
                                p.Communication, "", first ? 1 : 0, defaultBranch, createdon, updatedon);
                            persN++;
                            first = false;
                        }
                    }

                    tx.Commit();
                    PartyToContact[nkey] = ccode;
                    _cParties++; _cAddresses += addrN; _cPersons += persN;
                    if (_cParties % 500 == 0) Console.WriteLine("   ... " + _cParties + " contacts migrated");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        static int InsertAddress(SqlConnection tgt, SqlTransaction tx, int ccode, string place, string address, string area,
            int? cityCode, string pincode, string tel1, string tel2, string email1, string fax1, string website1,
            int isDefault, int isBillTo, int isShipTo, object createdon, object updatedon)
        {
            string sql = @"
                INSERT INTO mltaddress
                    (ccode, place, address, area, citycode, pincode, telephone1, telephone2, email1, fax1, website1,
                     isdefault, isbillto, isshipto, createdby, updatedby, createdon, updatedon)
                OUTPUT INSERTED.code
                VALUES (@ccode, @place, @addr, @area, @city, @zip, @t1, @t2, @email, @fax, @web,
                        @def, @bill, @ship, @cb, @cb, @con, @uon)";
            using (SqlCommand a = new SqlCommand(sql, tgt, tx))
            {
                a.Parameters.AddWithValue("@ccode", ccode);
                a.Parameters.AddWithValue("@place", PS(place));
                a.Parameters.AddWithValue("@addr", PS(address));
                a.Parameters.AddWithValue("@area", PS(area));
                a.Parameters.AddWithValue("@city", cityCode.HasValue ? (object)cityCode.Value : DBNull.Value);
                a.Parameters.AddWithValue("@zip", PS(pincode));
                a.Parameters.AddWithValue("@t1", PS(tel1));
                a.Parameters.AddWithValue("@t2", PS(tel2));
                a.Parameters.AddWithValue("@email", PS(email1));
                a.Parameters.AddWithValue("@fax", PS(fax1));
                a.Parameters.AddWithValue("@web", PS(website1));
                a.Parameters.AddWithValue("@def", isDefault);
                a.Parameters.AddWithValue("@bill", isBillTo);
                a.Parameters.AddWithValue("@ship", isShipTo);
                a.Parameters.AddWithValue("@cb", MigrationUser);
                a.Parameters.AddWithValue("@con", createdon);
                a.Parameters.AddWithValue("@uon", updatedon);
                return Convert.ToInt32(a.ExecuteScalar());
            }
        }

        static void InsertContactRow(SqlConnection tgt, SqlTransaction tx, int ccode, string name, string designation,
            string mobile, string tel1, string tel2, string email, string other,
            DateTime? bday, DateTime? anniv, DateTime? spdob, DateTime? c1, DateTime? c2,
            string communication, string department, int isDefault, int branch, object createdon, object updatedon)
        {
            string sql = @"
                INSERT INTO mltcontact
                    (ccode, name, designation, mobile1, telephone1, telephone2, email1, otherinfo, department, communication,
                     bday, anniversary, spousebday, firstchildbday, secondchildbday,
                     isdefault, branch, createdby, updatedby, createdon, updatedon)
                VALUES (@ccode, @name, @desig, @mob, @t1, @t2, @email, @other, @dept, @comm,
                        @bday, @anniv, @spdob, @c1, @c2, @def, @branch, @cb, @cb, @con, @uon)";
            using (SqlCommand c = new SqlCommand(sql, tgt, tx))
            {
                c.Parameters.AddWithValue("@ccode", ccode);
                c.Parameters.AddWithValue("@name", name);
                c.Parameters.AddWithValue("@desig", PS(designation));
                c.Parameters.AddWithValue("@mob", PS(mobile));
                c.Parameters.AddWithValue("@t1", PS(tel1));
                c.Parameters.AddWithValue("@t2", PS(tel2));
                c.Parameters.AddWithValue("@email", PS(email));
                c.Parameters.AddWithValue("@other", PS(other));
                c.Parameters.AddWithValue("@dept", PS(department));
                c.Parameters.AddWithValue("@comm", PS(communication));
                c.Parameters.AddWithValue("@bday", P(bday));
                c.Parameters.AddWithValue("@anniv", P(anniv));
                c.Parameters.AddWithValue("@spdob", P(spdob));
                c.Parameters.AddWithValue("@c1", P(c1));
                c.Parameters.AddWithValue("@c2", P(c2));
                c.Parameters.AddWithValue("@def", isDefault);
                c.Parameters.AddWithValue("@branch", branch > 0 ? (object)branch : DBNull.Value);
                c.Parameters.AddWithValue("@cb", MigrationUser);
                c.Parameters.AddWithValue("@con", createdon);
                c.Parameters.AddWithValue("@uon", updatedon);
                c.ExecuteNonQuery();
            }
        }

        static void LoadSourceMasters()
        {
            _titles = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _partyTypes = SrcLookupInt("SELECT ncode, vname FROM mstpartytype WITH (NOLOCK)");
            _partyProfiles = SrcLookupInt("SELECT ncode, vname FROM mstpartyprofile WITH (NOLOCK)");
            _routes = SrcLookupNKey("SELECT ncode, vname FROM mstroute WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
            _srcCityName = SrcLookupInt("SELECT ncode, vname FROM mstcity WITH (NOLOCK)");
            _users = SrcLookupNKey("SELECT ncode, vname FROM mstusers WITH (NOLOCK)");
            _designations = SrcLookupInt("SELECT ncode, vname FROM mstdesignation WITH (NOLOCK)");
        }

        // Seed miscellaneous / master tables that back the contact-form dropdowns.
        static void SeedDesignations(SqlConnection tgt)
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT name FROM desigmaster").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0) existing.Add(n); }

            int added = 0;
            foreach (DataRow r in GetSrc("SELECT DISTINCT ndesignation FROM msdparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND ndesignation IS NOT NULL AND ndesignation > 0").Tables[0].Rows)
            {
                string nm = LookInt(_designations, Int(r["ndesignation"]));
                if (nm.Length == 0 || existing.Contains(nm)) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO desigmaster (name, createdby, createdon, updatedby, updatedon) VALUES (@n, @cb, GETDATE(), @cb, GETDATE())", tgt))
                { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); ins.ExecuteNonQuery(); }
                existing.Add(nm); added++;
            }
            Console.WriteLine("  Designations seeded to desigmaster: " + added);
        }

        static void SeedCommunication(SqlConnection tgt)
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT name FROM miscellaneous WHERE module='Contact' AND type='Communication'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0) existing.Add(n); }

            int added = 0;
            foreach (DataRow r in GetSrc("SELECT DISTINCT ncommunication FROM msdparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND ncommunication IS NOT NULL AND ncommunication > 0").Tables[0].Rows)
            {
                string nm = LookInt(_titles, Int(r["ncommunication"]));
                if (nm.Length == 0 || existing.Contains(nm)) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) VALUES ('Contact','Communication', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); ins.ExecuteNonQuery(); }
                existing.Add(nm); added++;
            }
            Console.WriteLine("  Communication models seeded to miscellaneous(Contact/Communication): " + added);
        }

        static void SeedPartyTypes(SqlConnection tgt) { SeedContactMisc(tgt, "Party Type", "npartytype", _partyTypes); }
        static void SeedPartyProfiles(SqlConnection tgt) { SeedContactMisc(tgt, "Party Profile", "npartyprofile", _partyProfiles); }

        static void SeedContactMisc(SqlConnection tgt, string type, string srcCol, Dictionary<int, string> master)
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand c = new SqlCommand("SELECT name FROM miscellaneous WHERE module='Contact' AND type=@t", tgt))
            {
                c.Parameters.AddWithValue("@t", type);
                using (SqlDataReader r = c.ExecuteReader()) while (r.Read()) { string n = Str(r["name"]); if (n.Length > 0) existing.Add(n); }
            }

            int added = 0;
            foreach (DataRow r in GetSrc("SELECT DISTINCT " + srcCol + " FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND " + srcCol + " IS NOT NULL AND " + srcCol + " > 0").Tables[0].Rows)
            {
                string nm = LookInt(master, Int(r[srcCol]));
                if (nm.Length == 0 || existing.Contains(nm)) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) VALUES ('Contact', @t, @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                { ins.Parameters.AddWithValue("@t", type); ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); ins.ExecuteNonQuery(); }
                existing.Add(nm); added++;
            }
            Console.WriteLine("  " + type + " seeded to miscellaneous(Contact/" + type + "): " + added);
        }

        // Migrate ALL source users so every salesperson resolves to a real user (not Admin).
        static void EnsureUsers(SqlConnection tgt)
        {
            HashSet<string> usedUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand c = new SqlCommand("SELECT code, name, username FROM users", tgt))
            using (SqlDataReader r = c.ExecuteReader())
                while (r.Read())
                {
                    string nm = Str(r["name"]); if (nm.Length > 0 && !_userByName.ContainsKey(nm)) _userByName[nm] = Int(r["code"]);
                    string un = Str(r["username"]); if (un.Length > 0) usedUsernames.Add(un);
                }

            int tmplCompcode = 0, tmplRole = 0; string tmplPass = "";
            using (SqlCommand c = new SqlCommand("SELECT compcode, role, password FROM users WHERE code=@c", tgt))
            {
                c.Parameters.AddWithValue("@c", MigrationUser);
                using (SqlDataReader r = c.ExecuteReader())
                    if (r.Read()) { tmplCompcode = Int(r["compcode"]); tmplRole = Int(r["role"]); tmplPass = Str(r["password"]); }
            }

            int created = 0;
            foreach (KeyValuePair<string, string> kv in _users)
            {
                string nm = (kv.Value ?? "").Trim();
                if (nm.Length == 0) continue;
                if (nm.Length > 150) nm = nm.Substring(0, 150);
                if (_userByName.ContainsKey(nm)) continue;

                string baseun = new string(nm.Where(char.IsLetterOrDigit).ToArray());
                if (baseun.Length == 0) baseun = "user";
                if (baseun.Length > 40) baseun = baseun.Substring(0, 40);
                string un = baseun; int seq = 2;
                while (usedUsernames.Contains(un)) { un = baseun + seq; seq++; }
                usedUsernames.Add(un);

                using (SqlCommand ins = new SqlCommand(@"INSERT INTO users (name, username, password, compcode, role, createdby, createdon, updatedby, updatedon)
                                         OUTPUT INSERTED.code VALUES (@n, @un, @pw, @cc, @rl, @cb, GETDATE(), @cb, GETDATE())", tgt))
                {
                    ins.Parameters.AddWithValue("@n", nm);
                    ins.Parameters.AddWithValue("@un", un);
                    ins.Parameters.AddWithValue("@pw", PS(tmplPass));
                    ins.Parameters.AddWithValue("@cc", tmplCompcode == 0 ? (object)DBNull.Value : tmplCompcode);
                    ins.Parameters.AddWithValue("@rl", tmplRole == 0 ? (object)DBNull.Value : tmplRole);
                    ins.Parameters.AddWithValue("@cb", MigrationUser);
                    int code = Convert.ToInt32(ins.ExecuteScalar());
                    _userByName[nm] = code; created++;
                }
            }
            Console.WriteLine("  Users (ALL source users): " + _userByName.Count + " in target, " + created + " created.");
        }

        static string ResolveDesignation(int ndesig, string vdesig)
        {
            string m = LookInt(_designations, ndesig);
            return m.Length > 0 ? m : vdesig;
        }

        static void SeedCitiesAndStates(SqlConnection tgt)
        {
            Dictionary<string, int> countryMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM country WITH (NOLOCK)").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !countryMap.ContainsKey(n)) countryMap[n] = Int(r["code"]); }

            Dictionary<string, int> stateMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM state WITH (NOLOCK)").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !stateMap.ContainsKey(n)) stateMap[n] = Int(r["code"]); }

            _tgtCity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM city WITH (NOLOCK)").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !_tgtCity.ContainsKey(n)) _tgtCity[n] = Int(r["code"]); }

            int defaultCountryId;
            if (!countryMap.TryGetValue("India", out defaultCountryId))
            {
                using (SqlCommand insCo = new SqlCommand("INSERT INTO country (name, createdby, updatedby, createdon, updatedon, makedefault) OUTPUT INSERTED.code VALUES ('India', @cb, @cb, GETDATE(), GETDATE(), 0)", tgt))
                { insCo.Parameters.AddWithValue("@cb", MigrationUser); defaultCountryId = Convert.ToInt32(insCo.ExecuteScalar()); }
                countryMap["India"] = defaultCountryId; _newCountries++;
            }

            int defaultStateId;
            if (!stateMap.TryGetValue("Other", out defaultStateId))
            {
                using (SqlCommand insSt = new SqlCommand("INSERT INTO state (name, countrycode, createdby, updatedby, createdon, updatedon, makedefault) OUTPUT INSERTED.code VALUES ('Other', @cc, @cb, @cb, GETDATE(), GETDATE(), 0)", tgt))
                { insSt.Parameters.AddWithValue("@cc", defaultCountryId); insSt.Parameters.AddWithValue("@cb", MigrationUser); defaultStateId = Convert.ToInt32(insSt.ExecuteScalar()); }
                stateMap["Other"] = defaultStateId; _newStates++;
            }

            string srcSql = @"
                SELECT c.vname AS CityName, s.vname AS StateName, co.vname AS CountryName
                FROM mstcity c WITH (NOLOCK)
                LEFT JOIN mststate s WITH (NOLOCK) ON c.nstate = s.ncode
                LEFT JOIN mstcountry co WITH (NOLOCK) ON s.ncountry = co.ncode
                WHERE c.vname IS NOT NULL AND LTRIM(RTRIM(c.vname)) <> ''";

            foreach (DataRow row in GetSrc(srcSql).Tables[0].Rows)
            {
                string cityName = Str(row["CityName"]).Trim();
                if (cityName.Length == 0 || _tgtCity.ContainsKey(cityName)) continue;

                string stateName = Str(row["StateName"]).Trim();
                string countryName = Str(row["CountryName"]).Trim();

                int countryId = defaultCountryId;
                if (countryName.Length > 0 && !countryMap.TryGetValue(countryName, out countryId))
                {
                    using (SqlCommand insCo = new SqlCommand("INSERT INTO country (name, createdby, updatedby, createdon, updatedon, makedefault) OUTPUT INSERTED.code VALUES (@n, @cb, @cb, GETDATE(), GETDATE(), 0)", tgt))
                    { insCo.Parameters.AddWithValue("@n", countryName); insCo.Parameters.AddWithValue("@cb", MigrationUser); countryId = Convert.ToInt32(insCo.ExecuteScalar()); }
                    countryMap[countryName] = countryId; _newCountries++;
                }

                int stateId = defaultStateId;
                if (stateName.Length > 0 && !stateMap.TryGetValue(stateName, out stateId))
                {
                    using (SqlCommand insSt = new SqlCommand("INSERT INTO state (name, countrycode, createdby, updatedby, createdon, updatedon, makedefault) OUTPUT INSERTED.code VALUES (@n, @cc, @cb, @cb, GETDATE(), GETDATE(), 0)", tgt))
                    { insSt.Parameters.AddWithValue("@n", stateName); insSt.Parameters.AddWithValue("@cc", countryId); insSt.Parameters.AddWithValue("@cb", MigrationUser); stateId = Convert.ToInt32(insSt.ExecuteScalar()); }
                    stateMap[stateName] = stateId; _newStates++;
                }

                using (SqlCommand insCi = new SqlCommand("INSERT INTO city (name, countrycode, statecode, makedefault, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, @cc, @sc, 0, @cb, @cb, GETDATE(), GETDATE())", tgt))
                {
                    insCi.Parameters.AddWithValue("@n", cityName);
                    insCi.Parameters.AddWithValue("@cc", countryId);
                    insCi.Parameters.AddWithValue("@sc", stateId);
                    insCi.Parameters.AddWithValue("@cb", MigrationUser);
                    int cityCode = Convert.ToInt32(insCi.ExecuteScalar());
                    _tgtCity[cityName] = cityCode; _newCities++;
                }
            }
        }

        static int? ResolveCity(int srcCityId)
        {
            string nm;
            if (srcCityId <= 0 || !_srcCityName.TryGetValue(srcCityId, out nm)) return null;
            nm = (nm ?? "").Trim();
            if (nm.Length == 0) return null;
            int code;
            if (_tgtCity.TryGetValue(nm, out code)) return code;
            _unmatchedCities.Add(nm);
            return null;
        }

        static string PlaceFor(string area, int ncity, int office)
        {
            // place = standard "Place/Branch/Office" label. Source has no place field, so use city
            // (the natural location label); fall back to route (area), then office, then "Main Office".
            // Route stays in the area column (not duplicated into place).
            string p = "";
            string cn, on;
            if (ncity > 0 && _srcCityName.TryGetValue(ncity, out cn)) p = (cn ?? "").Trim();
            if (p.Length == 0) p = (area ?? "").Trim();
            if (p.Length == 0 && _officeName.TryGetValue(office, out on)) p = (on ?? "").Trim();
            if (p.Length == 0) p = "Main Office";
            return p.Length > 100 ? p.Substring(0, 100) : p;
        }

        static void EnsureBranches(SqlConnection tgt)
        {
            _branchByOffice = new Dictionary<int, int>();
            foreach (int office in Offices)
            {
                string nm;
                string place = _officeName.TryGetValue(office, out nm) && nm.Length > 0
                    ? (nm.Length > 100 ? nm.Substring(0, 100) : nm) : "Office " + office;

                int code;
                using (SqlCommand chk = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                {
                    chk.Parameters.AddWithValue("@cc", CompanyCode);
                    chk.Parameters.AddWithValue("@pl", place);
                    object o = chk.ExecuteScalar();
                    if (o != null && o != DBNull.Value) code = Convert.ToInt32(o);
                    else
                    {
                        using (SqlCommand ins = new SqlCommand("INSERT INTO companyaddress (ccode, place, isdefault, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@cc, @pl, 0, @cb, @cb, GETDATE(), GETDATE())", tgt))
                        {
                            ins.Parameters.AddWithValue("@cc", CompanyCode);
                            ins.Parameters.AddWithValue("@pl", place);
                            ins.Parameters.AddWithValue("@cb", MigrationUser);
                            code = Convert.ToInt32(ins.ExecuteScalar());
                            Console.WriteLine("  [branch] '" + place + "' -> companyaddress.code " + code);
                        }
                    }
                }
                _branchByOffice[office] = code;
            }
        }

        static void LoadPersons()
        {
            _persons = new Dictionary<string, List<Person>>();
            string sql = @"
                SELECT nparty, ntitle, vcontactperson, vdesignation, ndesignation, ncommunication, vmobile, vtel1, vtel2, vemail, vremarks,
                       ddob, danniversary, vspousename, dspousedob, dchild1dob, dchild2dob
                FROM msdparty WITH (NOLOCK)
                WHERE nofficeid IN (" + OfficeIn + @") AND LTRIM(RTRIM(ISNULL(vcontactperson,''))) <> ''
                ORDER BY ncode";
            foreach (DataRow r in GetSrc(sql).Tables[0].Rows)
            {
                string key = NKey(r["nparty"]);
                if (key.Length == 0) continue;
                Person p = new Person();
                p.TitleId = Int(r["ntitle"]);
                p.Name = Str(r["vcontactperson"]);
                p.Designation = ResolveDesignation(Int(r["ndesignation"]), Str(r["vdesignation"]));
                p.Communication = LookInt(_titles, Int(r["ncommunication"]));
                p.Mobile = Str(r["vmobile"]);
                p.Tel1 = Str(r["vtel1"]);
                p.Tel2 = Str(r["vtel2"]);
                p.Email = Str(r["vemail"]);
                p.Remarks = Str(r["vremarks"]);
                p.SpouseName = Str(r["vspousename"]);
                p.Dob = Date(r["ddob"]);
                p.Anniversary = Date(r["danniversary"]);
                p.SpouseDob = Date(r["dspousedob"]);
                p.Child1Dob = Date(r["dchild1dob"]);
                p.Child2Dob = Date(r["dchild2dob"]);
                List<Person> list;
                if (!_persons.TryGetValue(key, out list)) { list = new List<Person>(); _persons[key] = list; }
                list.Add(p);
            }
        }

        static void WriteUnmatchedCityLog()
        {
            if (_unmatchedCities.Count == 0) return;
            try { File.WriteAllLines(@"C:\FTFS\Maping\unmatched_cities.txt", _unmatchedCities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)); }
            catch { }
        }

        static string BuildRemark(string remark, string tax1, string tax2, string tax3)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(remark)) parts.Add(remark);
            if (!string.IsNullOrWhiteSpace(tax1)) parts.Add("Tax1: " + tax1);
            if (!string.IsNullOrWhiteSpace(tax2)) parts.Add("Tax2: " + tax2);
            if (!string.IsNullOrWhiteSpace(tax3)) parts.Add("Tax3: " + tax3);
            return string.Join(" | ", parts);
        }

        static string Join(string a, string b)
        {
            return string.Join(" ", new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        }

        static string Trim(string s, int n) { return string.IsNullOrEmpty(s) ? s : (s.Length > n ? s.Substring(0, n) : s); }

        class Person
        {
            public int TitleId;
            public string Name, Designation, Mobile, Tel1, Tel2, Email, Remarks, SpouseName, Communication;
            public DateTime? Dob, Anniversary, SpouseDob, Child1Dob, Child2Dob;
        }
    }

    // ==================================================================
    //  PRODUCT (Item Master) : mstitems/msditemsuppliers -> product/contprod
    //  Seeds prod_type/prodcat/unit/currency/Class; principal -> product.ccode; suppliers -> contprod 'S'.
    // ==================================================================
    static class Product
    {
        static Dictionary<int, string> _titles, _srcCategory, _srcUnit, _officeName;
        static Dictionary<int, ItemType> _srcItemType;
        static Dictionary<int, Curr> _srcCurrency;
        static Dictionary<string, int> _partyContact;
        static Dictionary<int, List<string>> _suppliers;
        static readonly Dictionary<int, int> _mapItemType = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _mapCategory = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _mapUnit = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _mapCurrency = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _branchByOffice = new Dictionary<int, int>();
        static int _cItems, _cContprod, _cSkipped, _cErrors, _newProdType, _newProdCat, _newUnit, _newCurrency, _ccodeResolved, _ccodeUnresolved;
        static int _defBrand, _defModel, _cBatch;   // inventory chain: Default Brand / model 'default' / Default Batch - Aftab Alam

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: PRODUCT");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            LoadSourceMasters();

            using (SqlConnection tgt = OpenTgt())
            {
                SeedProdType(tgt);
                SeedProdCat(tgt);
                SeedUnit(tgt);
                SeedCurrency(tgt);
                SeedProductClass(tgt);
                EnsureDefaultBrandModel(tgt);
                BuildPartyContactMap(tgt);
                EnsureBranches(tgt);
                LoadSuppliers();

                Console.WriteLine("Masters mapped: itemtype=" + _mapItemType.Count + "(+" + _newProdType + " new), category=" + _mapCategory.Count + "(+" + _newProdCat +
                                  "), unit=" + _mapUnit.Count + "(+" + _newUnit + "), currency=" + _mapCurrency.Count + "(+" + _newCurrency + ")");
                Console.WriteLine("Party->Contact names: " + _partyContact.Count + " | Suppliers grouped: " + _suppliers.Count + " items | Branches: " + _branchByOffice.Count);
                Console.WriteLine("-----------------------------------------------------");

                string sql = @"
                    SELECT ncode, vitemcode, vname, vprintdescription, nitemtype, nitemcategory, nunits, ncurrency,
                           nparty, npurchaseprice, nsellingprice, nwarrantyperiod, nnpmvisits,
                           ncontractamt1, ncontractamt2, ncontractamt3, ncontractamt4, nclass,
                           vremarks, bactive, nofficeid, addedon, editedon
                    FROM mstitems WITH (NOLOCK)
                    WHERE nofficeid IN (" + OfficeIn + @")
                    ORDER BY ncode";

                using (SqlConnection src = OpenSrc())
                using (SqlCommand rc = new SqlCommand(sql, src))
                {
                    rc.CommandTimeout = 0;
                    using (SqlDataReader dr = rc.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            try { MigrateItem(tgt, dr); }
                            catch (Exception ex) { _cErrors++; Console.WriteLine("  [ERROR] item ncode=" + Str(dr["ncode"]) + ": " + ex.Message); }
                        }
                    }
                }
            }

            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine("  Products migrated : " + _cItems);
            Console.WriteLine("  contprod rows     : " + _cContprod + "  (Principal + Suppliers)");
            Console.WriteLine("  Default Batch     : " + _cBatch + "  (brand code " + _defBrand + " / model code " + _defModel + ")");
            Console.WriteLine("  Principal ccode   : " + _ccodeResolved + " resolved, " + _ccodeUnresolved + " unresolved");
            Console.WriteLine("  Masters added     : prod_type " + _newProdType + ", prodcat " + _newProdCat + ", unit " + _newUnit + ", currency " + _newCurrency);
            Console.WriteLine("  Skipped (no name) : " + _cSkipped);
            Console.WriteLine("  Errors            : " + _cErrors);
            Console.WriteLine("-----------------------------------------------------");
        }

        static void MigrateItem(SqlConnection tgt, SqlDataReader dr)
        {
            string name = Cap(Str(dr["vname"]), 150);
            if (string.IsNullOrWhiteSpace(name)) { _cSkipped++; return; }

            string casno = Cap(Str(dr["vitemcode"]), 50);
            string profile = Str(dr["vprintdescription"]);
            string remark = BuildRemark(dr);
            string className = LookInt(_titles, Int(dr["nclass"]));

            int itemTypeId = Int(dr["nitemtype"]);
            int pt; object prodtype = _mapItemType.TryGetValue(itemTypeId, out pt) ? (object)pt : DBNull.Value;
            ItemType it; string type = _srcItemType.TryGetValue(itemTypeId, out it) && it.IsLabour ? "s" : "p";

            int pc; object prodcat = _mapCategory.TryGetValue(Int(dr["nitemcategory"]), out pc) ? (object)pc : DBNull.Value;
            int uc; object unitcode = _mapUnit.TryGetValue(Int(dr["nunits"]), out uc) ? (object)uc : DBNull.Value;
            int cc; object currency = _mapCurrency.TryGetValue(Int(dr["ncurrency"]), out cc) ? (object)cc : DBNull.Value;

            object price = dr["nsellingprice"] == DBNull.Value ? (object)DBNull.Value : dr["nsellingprice"];
            object costprice = dr["npurchaseprice"] == DBNull.Value ? (object)DBNull.Value : dr["npurchaseprice"];

            int isDeleted = Bool(dr["bactive"]) ? 0 : 1;
            int office = Int(dr["nofficeid"]);
            int b; object branch = _branchByOffice.TryGetValue(office, out b) ? (object)b.ToString(CultureInfo.InvariantCulture) : DBNull.Value;

            object createdon = P(dr["addedon"]);
            object updatedon = dr["editedon"] != DBNull.Value ? dr["editedon"] : P(dr["addedon"]);

            string partyKey = NKey(dr["nparty"]);
            object ccode = DBNull.Value; int ccodeInt = 0, cid;
            if (partyKey.Length > 0 && _partyContact.TryGetValue(partyKey, out cid)) { ccode = cid; ccodeInt = cid; _ccodeResolved++; }
            else if (partyKey.Length > 0 && Dec(dr["nparty"]) > 0) _ccodeUnresolved++;

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                int contprodN = 0;
                try
                {
                    int pcode;
                    string insP = @"
                        INSERT INTO product
                            (name, casno, profile, remark, grade, type, prodtype, prodcat, unitcode, currency,
                             price, minimumprice, ccode, companybranch, baseqty, isdeleted,
                             createdby, updatedby, createdon, updatedon)
                        OUTPUT INSERTED.code
                        VALUES (@name, @casno, @profile, @remark, @grade, @type, @prodtype, @prodcat, @unit, @curr,
                                @price, @cost, @ccode, @branch, 1, @isdel,
                                @cb, @cb, @con, @uon)";
                    using (SqlCommand c = new SqlCommand(insP, tgt, tx))
                    {
                        c.Parameters.AddWithValue("@name", name);
                        c.Parameters.AddWithValue("@casno", PS(casno));
                        c.Parameters.AddWithValue("@profile", PS(profile));
                        c.Parameters.AddWithValue("@remark", PS(remark));
                        c.Parameters.AddWithValue("@grade", PS(className));
                        c.Parameters.AddWithValue("@type", type);
                        c.Parameters.AddWithValue("@prodtype", prodtype);
                        c.Parameters.AddWithValue("@prodcat", prodcat);
                        c.Parameters.AddWithValue("@unit", unitcode);
                        c.Parameters.AddWithValue("@curr", currency);
                        c.Parameters.AddWithValue("@price", price);
                        c.Parameters.AddWithValue("@cost", costprice);
                        c.Parameters.AddWithValue("@ccode", ccode);
                        c.Parameters.AddWithValue("@branch", branch);
                        c.Parameters.AddWithValue("@isdel", isDeleted);
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        c.Parameters.AddWithValue("@con", createdon);
                        c.Parameters.AddWithValue("@uon", updatedon);
                        pcode = Convert.ToInt32(c.ExecuteScalar());
                    }

                    // Inventory chain, same as the product form (app/product.asp): product -> Default Brand -> model 'default'
                    // -> 'Default Batch'. Without it the product has no batch: it does not show in the Sales Invoice product
                    // search (selectjson ProductInvoice needs a batch) and invoice/PO lines have no batch. - Aftab Alam
                    CreateDefaultBatch(tgt, tx, pcode);

                    // Suppliers -> contprod 'S'. Principal (nparty) lives in product.ccode only; not a supplier.
                    HashSet<int> added = new HashSet<int>();
                    List<string> supKeys;
                    if (_suppliers.TryGetValue(Int(dr["ncode"]), out supKeys))
                    {
                        foreach (string sk in supKeys)
                        {
                            int scid;
                            if (_partyContact.TryGetValue(sk, out scid) && scid != ccodeInt && added.Add(scid))
                            { InsertContprod(tgt, tx, pcode, scid, "S"); contprodN++; }
                        }
                    }

                    tx.Commit();
                    _cItems++; _cContprod += contprodN;
                    if (_cItems % 1000 == 0) Console.WriteLine("   ... " + _cItems + " products migrated");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // Default Brand ('Default Brand') and model ('default') are masters shared by every product — reuse if present,
        // create only if missing (same names/lookups as include.asp GetDefaultBatch). - Aftab Alam
        static void EnsureDefaultBrandModel(SqlConnection tgt)
        {
            using (SqlCommand c = new SqlCommand("SELECT TOP 1 code FROM prodbrand WHERE name = 'Default Brand' ORDER BY code DESC", tgt))
            {
                object o = c.ExecuteScalar();
                if (o != null && o != DBNull.Value) _defBrand = Convert.ToInt32(o);
            }
            if (_defBrand == 0)
                using (SqlCommand c = new SqlCommand("INSERT INTO prodbrand (name, sname, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Default Brand', 'Default', @cb, GETDATE(), @cb, GETDATE())", tgt))
                { c.Parameters.AddWithValue("@cb", MigrationUser); _defBrand = Convert.ToInt32(c.ExecuteScalar()); }

            using (SqlCommand c = new SqlCommand("SELECT TOP 1 code FROM prodmodel WHERE modelname = 'default' ORDER BY code DESC", tgt))
            {
                object o = c.ExecuteScalar();
                if (o != null && o != DBNull.Value) _defModel = Convert.ToInt32(o);
            }
            if (_defModel == 0)
                using (SqlCommand c = new SqlCommand("INSERT INTO prodmodel (modelname, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('default', @cb, GETDATE(), @cb, GETDATE())", tgt))
                { c.Parameters.AddWithValue("@cb", MigrationUser); _defModel = Convert.ToInt32(c.ExecuteScalar()); }

            Console.WriteLine("  Default Brand code " + _defBrand + ", model 'default' code " + _defModel);
        }

        // product -> prodbrandrelation -> prodbrandmodel -> prodbatch 'Default Batch' (reuses any part that already exists). - Aftab Alam
        static void CreateDefaultBatch(SqlConnection tgt, SqlTransaction tx, int pcode)
        {
            object o;
            using (SqlCommand c = new SqlCommand("SELECT TOP 1 pb.code FROM prodbatch pb JOIN prodbrandmodel pbm ON pb.pbmcode = pbm.code JOIN prodbrandrelation pbr ON pbm.pbcode = pbr.code WHERE pb.batchno = 'Default Batch' AND pbr.prodcode = @p", tgt, tx))
            { c.Parameters.AddWithValue("@p", pcode); o = c.ExecuteScalar(); }
            if (o != null && o != DBNull.Value) return;

            int relation;
            using (SqlCommand c = new SqlCommand("SELECT TOP 1 code FROM prodbrandrelation WHERE prodcode = @p AND brandcode = @b ORDER BY code DESC", tgt, tx))
            { c.Parameters.AddWithValue("@p", pcode); c.Parameters.AddWithValue("@b", _defBrand); o = c.ExecuteScalar(); }
            if (o != null && o != DBNull.Value) relation = Convert.ToInt32(o);
            else
                using (SqlCommand c = new SqlCommand("INSERT INTO prodbrandrelation (prodcode, brandcode, createdon, createdby, updatedon, updatedby) OUTPUT INSERTED.code VALUES (@p, @b, GETDATE(), @cb, GETDATE(), @cb)", tgt, tx))
                { c.Parameters.AddWithValue("@p", pcode); c.Parameters.AddWithValue("@b", _defBrand); c.Parameters.AddWithValue("@cb", MigrationUser); relation = Convert.ToInt32(c.ExecuteScalar()); }

            int brandModel;
            using (SqlCommand c = new SqlCommand("SELECT TOP 1 code FROM prodbrandmodel WHERE pbcode = @pb AND modelcode = @m ORDER BY code DESC", tgt, tx))
            { c.Parameters.AddWithValue("@pb", relation); c.Parameters.AddWithValue("@m", _defModel); o = c.ExecuteScalar(); }
            if (o != null && o != DBNull.Value) brandModel = Convert.ToInt32(o);
            else
                using (SqlCommand c = new SqlCommand("INSERT INTO prodbrandmodel (pbcode, modelcode, createdon, createdby, updatedon, updatedby, rol) OUTPUT INSERTED.code VALUES (@pb, @m, GETDATE(), @cb, GETDATE(), @cb, 1)", tgt, tx))
                { c.Parameters.AddWithValue("@pb", relation); c.Parameters.AddWithValue("@m", _defModel); c.Parameters.AddWithValue("@cb", MigrationUser); brandModel = Convert.ToInt32(c.ExecuteScalar()); }

            using (SqlCommand c = new SqlCommand("INSERT INTO prodbatch (pbmcode, batchno, expdate, mfgdate, unit, rate, openingbalance, sname, damagedqty) VALUES (@pbm, 'Default Batch', GETDATE(), GETDATE(), NULL, 0, 0, '', 0)", tgt, tx))
            { c.Parameters.AddWithValue("@pbm", brandModel); c.ExecuteNonQuery(); }
            _cBatch++;
        }

        static void InsertContprod(SqlConnection tgt, SqlTransaction tx, int pcode, int contactCode, string type)
        {
            using (SqlCommand c = new SqlCommand("INSERT INTO contprod (productcode, contactcode, type) VALUES (@p, @c, @t)", tgt, tx))
            {
                c.Parameters.AddWithValue("@p", pcode);
                c.Parameters.AddWithValue("@c", contactCode);
                c.Parameters.AddWithValue("@t", type);
                c.ExecuteNonQuery();
            }
        }

        static void LoadSourceMasters()
        {
            _titles = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
            _srcCategory = SrcLookupInt("SELECT ncode, vname FROM mstitemcategory WITH (NOLOCK)");
            _srcUnit = SrcLookupInt("SELECT ncode, vname FROM mstunits WITH (NOLOCK)");

            _srcItemType = new Dictionary<int, ItemType>();
            foreach (DataRow r in GetSrc("SELECT ncode, vname, blabour FROM mstitemtype WITH (NOLOCK)").Tables[0].Rows)
                _srcItemType[Int(r["ncode"])] = new ItemType { Name = Str(r["vname"]), IsLabour = Bool(r["blabour"]) };

            _srcCurrency = new Dictionary<int, Curr>();
            foreach (DataRow r in GetSrc("SELECT ncode, vmajordenomination, vmajorshortname FROM mstcurrency WITH (NOLOCK)").Tables[0].Rows)
                _srcCurrency[Int(r["ncode"])] = new Curr { Name = Str(r["vmajordenomination"]), Sname = Str(r["vmajorshortname"]) };
        }

        static void SeedProdType(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM prod_type").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (KeyValuePair<int, ItemType> kv in _srcItemType)
            {
                string n = (kv.Value.Name ?? "").Trim();
                if (n.Length == 0) continue;
                int code;
                if (!tmap.TryGetValue(n, out code))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO prod_type (name, makedefault, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, 0, @cb, @cb, GETDATE(), GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", n); ins.Parameters.AddWithValue("@cb", MigrationUser); code = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[n] = code; _newProdType++;
                }
                _mapItemType[kv.Key] = code;
            }
        }

        static void SeedProdCat(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM prodcat WHERE type = 'M'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (KeyValuePair<int, string> kv in _srcCategory)
            {
                string n = (kv.Value ?? "").Trim();
                if (n.Length == 0) continue;
                int code;
                if (!tmap.TryGetValue(n, out code))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO prodcat (name, type, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, 'M', @cb, @cb, GETDATE(), GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", n); ins.Parameters.AddWithValue("@cb", MigrationUser); code = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[n] = code; _newProdCat++;
                }
                _mapCategory[kv.Key] = code;
            }
        }

        static void SeedUnit(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM unit").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (KeyValuePair<int, string> kv in _srcUnit)
            {
                string n = (kv.Value ?? "").Trim();
                if (n.Length == 0) continue;
                int code;
                if (!tmap.TryGetValue(n, out code))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO unit (name, makedefault, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, 0, @cb, @cb, GETDATE(), GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", n); ins.Parameters.AddWithValue("@cb", MigrationUser); code = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[n] = code; _newUnit++;
                }
                _mapUnit[kv.Key] = code;
            }
        }

        static void SeedCurrency(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> bySname = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name, sname FROM currency").Tables[0].Rows)
            {
                int code = Int(r["code"]); string n = Str(r["name"]), s = Str(r["sname"]);
                if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = code;
                if (s.Length > 0 && !bySname.ContainsKey(s)) bySname[s] = code;
            }

            foreach (KeyValuePair<int, Curr> kv in _srcCurrency)
            {
                string n = (kv.Value.Name ?? "").Trim(), s = (kv.Value.Sname ?? "").Trim();
                int code;
                if (n.Length > 0 && byName.TryGetValue(n, out code)) { _mapCurrency[kv.Key] = code; continue; }
                if (s.Length > 0 && bySname.TryGetValue(s, out code)) { _mapCurrency[kv.Key] = code; continue; }
                if (n.Length == 0 && s.Length == 0) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO currency (name, sname, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, @s, @cb, @cb, GETDATE(), GETDATE())", tgt))
                {
                    ins.Parameters.AddWithValue("@n", PS(n.Length > 0 ? n : s));
                    ins.Parameters.AddWithValue("@s", PS(s.Length > 0 ? s : n));
                    ins.Parameters.AddWithValue("@cb", MigrationUser);
                    code = Convert.ToInt32(ins.ExecuteScalar());
                }
                byName[n] = code; if (s.Length > 0) bySname[s] = code;
                _mapCurrency[kv.Key] = code; _newCurrency++;
            }
        }

        static void SeedProductClass(SqlConnection tgt)
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT name FROM miscellaneous WHERE module='Product' AND type='Class'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0) existing.Add(n); }

            int added = 0;
            foreach (DataRow r in GetSrc("SELECT DISTINCT nclass FROM mstitems WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND nclass IS NOT NULL AND nclass > 0").Tables[0].Rows)
            {
                string nm = LookInt(_titles, Int(r["nclass"]));
                if (nm.Length == 0 || existing.Contains(nm)) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) VALUES ('Product','Class', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); ins.ExecuteNonQuery(); }
                existing.Add(nm); added++;
            }
            Console.WriteLine("  Product Class seeded to miscellaneous(Product/Class): " + added);
        }

        static void BuildPartyContactMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM contact WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows)
            { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }

            _partyContact = new Dictionary<string, int>();
            foreach (DataRow r in GetSrc("SELECT ncode, ntitle, vname FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = NKey(r["ncode"]); if (key.Length == 0) continue;
                string nm = Cap(Join(LookInt(_titles, Int(r["ntitle"])), Str(r["vname"])), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_partyContact.ContainsKey(key)) _partyContact[key] = code;
            }
            // Prefer the exact party->contact link Contact recorded during insert.
            foreach (KeyValuePair<string, int> kv in PartyToContact) _partyContact[kv.Key] = kv.Value;
        }

        static void EnsureBranches(SqlConnection tgt)
        {
            foreach (int office in Offices)
            {
                string nm;
                string place = _officeName.TryGetValue(office, out nm) && nm.Length > 0 ? (nm.Length > 100 ? nm.Substring(0, 100) : nm) : "Office " + office;
                using (SqlCommand chk = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                {
                    chk.Parameters.AddWithValue("@cc", CompanyCode);
                    chk.Parameters.AddWithValue("@pl", place);
                    object o = chk.ExecuteScalar();
                    if (o != null && o != DBNull.Value) _branchByOffice[office] = Convert.ToInt32(o);
                }
            }
        }

        static void LoadSuppliers()
        {
            _suppliers = new Dictionary<int, List<string>>();
            // NO office filter: nitem is global-unique and nparty is office-encoded; scope is enforced by
            // item resolution (_prodByItem) and party resolution (_partyContact). The link's own nofficeid
            // is irrelevant (e.g. an office-2 supplier link recorded under office 1 must still migrate).
            foreach (DataRow r in GetSrc("SELECT nitem, nparty FROM msditemsuppliers WITH (NOLOCK) WHERE nparty IS NOT NULL AND nparty > 0").Tables[0].Rows)
            {
                int item = Int(r["nitem"]); if (item <= 0) continue;
                string pk = NKey(r["nparty"]); if (pk.Length == 0) continue;
                List<string> list;
                if (!_suppliers.TryGetValue(item, out list)) { list = new List<string>(); _suppliers[item] = list; }
                if (!list.Contains(pk)) list.Add(pk);
            }
        }

        static string BuildRemark(SqlDataReader dr)
        {
            List<string> parts = new List<string>();
            string rem = Str(dr["vremarks"]); if (!string.IsNullOrWhiteSpace(rem)) parts.Add(rem);
            int warr = Int(dr["nwarrantyperiod"]); if (warr > 0) parts.Add("Warranty(months): " + warr);
            int npm = Int(dr["nnpmvisits"]); if (npm > 0) parts.Add("Maintenance Visits: " + npm);
            int[] ca = { Int(dr["ncontractamt1"]), Int(dr["ncontractamt2"]), Int(dr["ncontractamt3"]), Int(dr["ncontractamt4"]) };
            if (ca.Any(x => x > 0)) parts.Add("Contract Amt 1-4: " + string.Join("/", ca));
            return string.Join(" | ", parts);
        }

        static string Join(string a, string b) { return string.Join(" ", new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(); }

        class ItemType { public string Name; public bool IsLabour; }
        class Curr { public string Name; public string Sname; }
    }

    // ==================================================================
    //  CUSTOMER INQUIRY : trhinqry/trdinqry1prods -> inqcs (cors='CI')/inqcsdet
    //  Also migrates the action/follow-up timeline (trdinqry2actions -> followup) via nested Followup.
    // ==================================================================
    static class CustomerInquiry
    {
        public class InqTarget { public int Code; public int Cs; public string Party; public string ActionType; }

        static Dictionary<int, string> _fixed, _inqCategory, _inqSource, _lostReason, _salesStage, _officeName;
        static Dictionary<int, int> _mapStatus = new Dictionary<int, int>();
        static Dictionary<int, int> _mapCategory = new Dictionary<int, int>();
        static Dictionary<int, int> _mapSalesStage = new Dictionary<int, int>();
        static Dictionary<int, int> _mapLostReason = new Dictionary<int, int>();
        static Dictionary<int, int> _mapCallType = new Dictionary<int, int>();
        static Dictionary<string, int> _custByParty = new Dictionary<string, int>();
        static readonly Dictionary<int, int> _branchByContact = new Dictionary<int, int>();
        static Dictionary<int, int> _prodByItem = new Dictionary<int, int>();
        static Dictionary<int, int> _branchByOffice = new Dictionary<int, int>();
        static Dictionary<string, string> _persons = new Dictionary<string, string>();
        static Dictionary<string, List<int[]>> _inqProducts = new Dictionary<string, List<int[]>>();
        static readonly Dictionary<string, InqTarget> InqByKey = new Dictionary<string, InqTarget>();
        static int _cInq, _cDet, _cDetSkipped, _cErrors, _custResolved, _custUnresolved, _newStatus, _newCategory;

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: Customer Inquiry (inqcs cors='CI')");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            LoadSourceMasters();

            using (SqlConnection tgt = OpenTgt())
            {
                EnsureStatuses(tgt);
                SeedCategories(tgt);
                SeedSalesStageAndLostReason(tgt);
                SeedInqMiscMap(tgt, "Inquiry Source", "ninquirysource", "trhinqry", _inqSource, true);
                BuildCustomerMap(tgt);
                BuildBranchByContact(tgt);
                EnsureUserByCode();
                BuildProductMap(tgt);
                LoadPersons(tgt);
                LoadInquiryProducts();
                EnsureBranches(tgt);

                Console.WriteLine("Maps: status=" + _mapStatus.Count + "(+" + _newStatus + "), category=" + _mapCategory.Count + "(+" + _newCategory +
                                  "), customers=" + _custByParty.Count + ", products=" + _prodByItem.Count + ", persons=" + _persons.Count + ", inqWithProducts=" + _inqProducts.Count);
                Console.WriteLine("-----------------------------------------------------");

                string sql = @"
                    SELECT ncode, vtrnprefix, ntrnno, dtrndate, nparty, npartycontact, ninquiryby, vinquirydetails,
                           ninquirycategory, nsalesstage, ninquirystatus, nlostreasons, ntrhcampa, ninquirysource,
                           nsalesman, dclosingdate, vremarks, vcomment, nprobability, ninqvalue, nactionType,
                           nofficeid, addedon, editedon
                    FROM trhinqry WITH (NOLOCK)
                    WHERE nofficeid IN (" + OfficeIn + @")
                    ORDER BY ncode";

                using (SqlConnection src = OpenSrc())
                using (SqlCommand rc = new SqlCommand(sql, src))
                {
                    rc.CommandTimeout = 0;
                    using (SqlDataReader dr = rc.ExecuteReader())
                        while (dr.Read())
                        {
                            try { MigrateInquiry(tgt, dr); }
                            catch (Exception ex) { _cErrors++; Console.WriteLine("  [ERROR] customer inquiry ncode=" + Str(dr["ncode"]) + ": " + ex.Message); }
                        }
                }

                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("  Customer Inquiries migrated : " + _cInq);
                Console.WriteLine("  inqcsdet products  : " + _cDet + "  (skipped " + _cDetSkipped + ")");
                Console.WriteLine("  Customer cscode    : " + _custResolved + " resolved, " + _custUnresolved + " unresolved");
                Console.WriteLine("  Masters added      : status " + _newStatus + ", category " + _newCategory);
                Console.WriteLine("  Errors             : " + _cErrors);
                Console.WriteLine("-----------------------------------------------------");

                // Action / follow-up timeline (trdinqry2actions -> followup).
                Followup.Migrate(tgt, InqByKey);
            }
        }

        static void MigrateInquiry(SqlConnection tgt, SqlDataReader dr)
        {
            int ncode = Int(dr["ncode"]);
            string inqref = (Str(dr["vtrnprefix"]) + Str(dr["ntrnno"])).Trim();

            string partyKey = NKey(dr["nparty"]);
            object cscode = DBNull.Value, csbranch = DBNull.Value;
            int csInt = 0, cid;
            if (partyKey.Length > 0 && _custByParty.TryGetValue(partyKey, out cid))
            {
                cscode = cid; csInt = cid; _custResolved++;
                int br; if (_branchByContact.TryGetValue(cid, out br)) csbranch = br;
            }
            else if (partyKey.Length > 0 && Dec(dr["nparty"]) > 0) _custUnresolved++;

            string cperson = "";
            int personNo = Int(dr["npartycontact"]);
            if (personNo > 0 && partyKey.Length > 0) _persons.TryGetValue(partyKey + "|" + personNo, out cperson);

            int st; object status = _mapStatus.TryGetValue(Int(dr["ninquirystatus"]), out st) ? (object)st : DBNull.Value;
            int ca; object category = _mapCategory.TryGetValue(Int(dr["ninquirycategory"]), out ca) ? (object)ca : DBNull.Value;
            string sources = LookInt(_inqSource, Int(dr["ninquirysource"]));
            string types = LookInt(_fixed, Int(dr["nactionType"]));

            int office = Int(dr["nofficeid"]);
            int b; object branch = _branchByOffice.TryGetValue(office, out b) ? (object)b : DBNull.Value;

            object inqdate = P(dr["dtrndate"]);
            object validdate = P(dr["dclosingdate"]);
            object createdon = P(dr["addedon"]);
            object updatedon = dr["editedon"] != DBNull.Value ? dr["editedon"] : P(dr["addedon"]);

            string remark = BuildRemark(dr);
            int stcode; object stage = _mapSalesStage.TryGetValue(Int(dr["nsalesstage"]), out stcode) ? (object)stcode : DBNull.Value;
            int lrcode; object lossreason = _mapLostReason.TryGetValue(Int(dr["nlostreasons"]), out lrcode) ? (object)lrcode : DBNull.Value;
            int ctcode; object calltype = _mapCallType.TryGetValue(Int(dr["ninquiryby"]), out ctcode) ? (object)ctcode : DBNull.Value;

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                int detN = 0, detSkip = 0;
                try
                {
                    int code;
                    string ins = @"
                        INSERT INTO inqcs
                            (cors, inqref, inqdate, validdate, cscode, csbranch, cperson, executive, status, Category, sources,
                             types, stage, lossreason, calltype, comcode, branchcode, remark, createdby, updatedby, createdon, updatedon)
                        OUTPUT INSERTED.code
                        VALUES ('CI', @inqref, @inqdate, @validdate, @cscode, @csbranch, @cperson, @exec, @status, @cat, @sources,
                                @types, @stage, @lossreason, @calltype, @com, @branch, @remark, @cb, @cb, @con, @uon)";
                    using (SqlCommand c = new SqlCommand(ins, tgt, tx))
                    {
                        int _ex;
                        c.Parameters.AddWithValue("@inqref", PS(inqref));
                        c.Parameters.AddWithValue("@inqdate", inqdate);
                        c.Parameters.AddWithValue("@validdate", validdate);
                        c.Parameters.AddWithValue("@cscode", cscode);
                        c.Parameters.AddWithValue("@csbranch", csbranch);
                        c.Parameters.AddWithValue("@cperson", PS(cperson));
                        c.Parameters.AddWithValue("@exec", UserByCode.TryGetValue(NKey(dr["nsalesman"]), out _ex) ? (object)_ex : MigrationUser);
                        c.Parameters.AddWithValue("@status", status);
                        c.Parameters.AddWithValue("@cat", category);
                        c.Parameters.AddWithValue("@sources", PS(sources));
                        c.Parameters.AddWithValue("@types", PS(types));
                        c.Parameters.AddWithValue("@stage", stage);
                        c.Parameters.AddWithValue("@lossreason", lossreason);
                        c.Parameters.AddWithValue("@calltype", calltype);
                        c.Parameters.AddWithValue("@com", CompanyCode);
                        c.Parameters.AddWithValue("@branch", branch);
                        c.Parameters.AddWithValue("@remark", PS(remark));
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        c.Parameters.AddWithValue("@con", createdon);
                        c.Parameters.AddWithValue("@uon", updatedon);
                        code = Convert.ToInt32(c.ExecuteScalar());
                    }

                    InqTarget t = new InqTarget();
                    t.Code = code; t.Cs = csInt; t.Party = partyKey; t.ActionType = types;
                    InqByKey[office + "|" + ncode] = t;

                    List<int[]> plist;
                    if (_inqProducts.TryGetValue(office + "|" + ncode, out plist))
                    {
                        foreach (int[] p in plist)
                        {
                            int pcode;
                            if (!_prodByItem.TryGetValue(p[0], out pcode)) { detSkip++; continue; }
                            decimal qty = p[1] / 1000m;
                            using (SqlCommand d = new SqlCommand(@"INSERT INTO inqcsdet (inqcode, pcode, quantity, createdby, updatedby, createdon, updatedon)
                                                   VALUES (@i, @p, @q, @cb, @cb, @con, @uon)", tgt, tx))
                            {
                                d.Parameters.AddWithValue("@i", code);
                                d.Parameters.AddWithValue("@p", pcode);
                                d.Parameters.AddWithValue("@q", qty);
                                d.Parameters.AddWithValue("@cb", MigrationUser);
                                d.Parameters.AddWithValue("@con", createdon);
                                d.Parameters.AddWithValue("@uon", updatedon);
                                d.ExecuteNonQuery();
                            }
                            detN++;
                        }
                    }

                    tx.Commit();
                    _cInq++; _cDet += detN; _cDetSkipped += detSkip;
                    if (_cInq % 2000 == 0) Console.WriteLine("   ... " + _cInq + " customer inquiries migrated");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        static void LoadSourceMasters()
        {
            _fixed = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _inqCategory = SrcLookupInt("SELECT ncode, vname FROM mstinquirycategory WITH (NOLOCK)");
            _inqSource = SrcLookupInt("SELECT ncode, vname FROM mstinquirysource WITH (NOLOCK)");
            _lostReason = SrcLookupInt("SELECT ncode, vname FROM mstorderlostreason WITH (NOLOCK)");
            _salesStage = SrcLookupInt("SELECT ncode, vname FROM mstsalesstage WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
        }

        static void SeedSalesStageAndLostReason(SqlConnection tgt)
        {
            _mapSalesStage = SeedInqMiscMap(tgt, "Sales Stage", "nsalesstage", "trhinqry", _salesStage, true);
            _mapLostReason = SeedInqMiscMap(tgt, "Loss Reason", "nlostreasons", "trhinqry", _lostReason, true);
            _mapCallType = SeedInqMiscMap(tgt, "Call Type", "ninquiryby", "trhinqry", _fixed, false);
        }

        // seedFull=true: seed EVERY value of the source master (full parity with the client's form dropdown,
        // even values not used in the migrated data). seedFull=false: only the values actually used (for masters
        // like Call Type whose 'master' arg is the whole mstfixedselection, so we must NOT seed all of it). - Aftab Alam
        static Dictionary<int, int> SeedInqMiscMap(SqlConnection tgt, string type, string col, string table, Dictionary<int, string> master, bool seedFull)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand c = new SqlCommand("SELECT code, name FROM miscellaneous WHERE module='Inquiry' AND type=@t", tgt))
            {
                c.Parameters.AddWithValue("@t", type);
                using (SqlDataReader r = c.ExecuteReader())
                    while (r.Read()) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }
            }

            Dictionary<int, int> map = new Dictionary<int, int>();
            int added = 0;

            if (seedFull)
            {
                // Seed the entire source master so the form shows all options the client has.
                foreach (KeyValuePair<int, string> kv in master)
                {
                    string nm = (kv.Value ?? "").Trim();
                    if (nm.Length == 0) continue;
                    int miscCode;
                    if (!byName.TryGetValue(nm, out miscCode))
                    {
                        using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Inquiry', @t, @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                        { ins.Parameters.AddWithValue("@t", type); ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); miscCode = Convert.ToInt32(ins.ExecuteScalar()); }
                        byName[nm] = miscCode; added++;
                    }
                    map[kv.Key] = miscCode;
                }
            }
            else
            {
                foreach (DataRow row in GetSrc("SELECT DISTINCT " + col + " AS v FROM " + table + " WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND " + col + " IS NOT NULL AND " + col + " > 0").Tables[0].Rows)
                {
                    int srcCode = Int(row["v"]);
                    string nm = LookInt(master, srcCode);
                    if (nm.Length == 0) continue;
                    int miscCode;
                    if (!byName.TryGetValue(nm, out miscCode))
                    {
                        using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Inquiry', @t, @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                        { ins.Parameters.AddWithValue("@t", type); ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); miscCode = Convert.ToInt32(ins.ExecuteScalar()); }
                        byName[nm] = miscCode; added++;
                    }
                    map[srcCode] = miscCode;
                }
            }
            Console.WriteLine("  miscellaneous(Inquiry/" + type + ") seeded: " + added + (seedFull ? " (full master)" : ""));
            return map;
        }

        static void EnsureStatuses(SqlConnection tgt)
        {
            // CI status dropdown reads status where module='Inquiry' (app/inquiry.asp) — match/seed under 'Inquiry' only. - Aftab Alam
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM status WHERE module='Inquiry'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (DataRow row in GetSrc("SELECT DISTINCT ninquirystatus FROM trhinqry WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND ninquirystatus IS NOT NULL").Tables[0].Rows)
            {
                int scode = Int(row["ninquirystatus"]);
                string nm = LookInt(_fixed, scode);
                if (nm.Length == 0) continue;
                int tc;
                if (!tmap.TryGetValue(nm, out tc))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO status (name, module, sort, makedefault, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, 'Inquiry', 0, 0, @cb, @cb, GETDATE(), GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); tc = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[nm] = tc; _newStatus++;
                }
                _mapStatus[scode] = tc;
            }
        }

        static void SeedCategories(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM miscellaneous WHERE module='Inquiry' AND type='Category'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (KeyValuePair<int, string> kv in _inqCategory)
            {
                string n = (kv.Value ?? "").Trim();
                if (n.Length == 0) continue;
                int code;
                if (!tmap.TryGetValue(n, out code))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Inquiry','Category', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", n); ins.Parameters.AddWithValue("@cb", MigrationUser); code = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[n] = code; _newCategory++;
                }
                _mapCategory[kv.Key] = code;
            }
        }

        static void BuildCustomerMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM contact WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }

            foreach (DataRow r in GetSrc("SELECT ncode, ntitle, vname FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = NKey(r["ncode"]); if (key.Length == 0) continue;
                string nm = Cap(Join(LookInt(_fixed, Int(r["ntitle"])), Str(r["vname"])), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_custByParty.ContainsKey(key)) _custByParty[key] = code;
            }
            foreach (KeyValuePair<string, int> kv in PartyToContact) _custByParty[kv.Key] = kv.Value;
        }

        static void BuildBranchByContact(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, ccode, isnull(isdefault,0) as isdefault FROM mltaddress WITH (NOLOCK) ORDER BY ccode, isdefault DESC, code").Tables[0].Rows)
            {
                int ccode = Int(r["ccode"]); if (ccode <= 0) continue;
                if (!_branchByContact.ContainsKey(ccode)) _branchByContact[ccode] = Int(r["code"]);
            }
        }

        static void BuildProductMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM product WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }

            foreach (DataRow r in GetSrc("SELECT ncode, vname FROM mstitems WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                int ncode = Int(r["ncode"]);
                string nm = Cap(Str(r["vname"]), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_prodByItem.ContainsKey(ncode)) _prodByItem[ncode] = code;
            }
        }

        static void LoadPersons(SqlConnection tgt)
        {
            _persons = new Dictionary<string, string>();
            Dictionary<string, int> mltMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, ccode, name FROM mltcontact WITH (NOLOCK)").Tables[0].Rows)
            {
                int code = Int(r["code"]), ccode = Int(r["ccode"]); string name = Str(r["name"]);
                if (code > 0 && ccode > 0 && name.Length > 0) { string key = ccode + "|" + name; if (!mltMap.ContainsKey(key)) mltMap[key] = code; }
            }

            foreach (DataRow dr in GetSrc("SELECT nparty, ncode, ntitle, vcontactperson FROM msdparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND LTRIM(RTRIM(ISNULL(vcontactperson,''))) <> ''").Tables[0].Rows)
            {
                string partyKey = NKey(dr["nparty"]); if (partyKey.Length == 0) continue;
                int personNo = Int(dr["ncode"]); if (personNo <= 0) continue;
                string key = partyKey + "|" + personNo;
                if (_persons.ContainsKey(key)) continue;

                string rawName = Str(dr["vcontactperson"]);
                string title = LookInt(_fixed, Int(dr["ntitle"]));
                string formattedName = Cap(Join(title, rawName), 100);

                string mltCodeStr = "";
                int ccode2, mcode, mcode2;
                if (_custByParty.TryGetValue(partyKey, out ccode2))
                {
                    if (mltMap.TryGetValue(ccode2 + "|" + formattedName, out mcode)) mltCodeStr = mcode.ToString();
                    else if (mltMap.TryGetValue(ccode2 + "|" + rawName, out mcode2)) mltCodeStr = mcode2.ToString();
                }
                _persons[key] = mltCodeStr;
            }
        }

        static void LoadInquiryProducts()
        {
            foreach (DataRow r in GetSrc("SELECT nofficeid, ninquiry, nitem, nquantity FROM trdinqry1prods WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND nitem IS NOT NULL AND nitem > 0").Tables[0].Rows)
            {
                int inq = Int(r["ninquiry"]); if (inq <= 0) continue;
                string key = Int(r["nofficeid"]) + "|" + inq;
                int item = Int(r["nitem"]);
                int qtyx1000 = (int)Math.Round(Dec(r["nquantity"]) * 1000m);
                List<int[]> list;
                if (!_inqProducts.TryGetValue(key, out list)) { list = new List<int[]>(); _inqProducts[key] = list; }
                list.Add(new[] { item, qtyx1000 });
            }
        }

        static void EnsureBranches(SqlConnection tgt)
        {
            foreach (int office in Offices)
            {
                string nm;
                string place = _officeName.TryGetValue(office, out nm) && nm.Length > 0 ? (nm.Length > 100 ? nm.Substring(0, 100) : nm) : "Office " + office;
                using (SqlCommand chk = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                {
                    chk.Parameters.AddWithValue("@cc", CompanyCode);
                    chk.Parameters.AddWithValue("@pl", place);
                    object o = chk.ExecuteScalar();
                    if (o != null && o != DBNull.Value) _branchByOffice[office] = Convert.ToInt32(o);
                }
            }
        }

        static string BuildRemark(SqlDataReader dr)
        {
            List<string> parts = new List<string>();
            string d1 = Str(dr["vinquirydetails"]); if (!string.IsNullOrWhiteSpace(d1)) parts.Add(d1);
            string d2 = Str(dr["vremarks"]); if (!string.IsNullOrWhiteSpace(d2)) parts.Add(d2);
            string cm = Str(dr["vcomment"]); if (!string.IsNullOrWhiteSpace(cm)) parts.Add("Comment: " + cm);
            int prob = Int(dr["nprobability"]); if (prob > 0) parts.Add("Probability %: " + prob);
            int iv = Int(dr["ninqvalue"]); if (iv > 0) parts.Add("Inquiry Value: " + iv);
            int camp = Int(dr["ntrhcampa"]); if (camp > 0) parts.Add("Campaign: " + camp);
            return string.Join(" | ", parts);
        }

        static string Join(string a, string b) { return string.Join(" ", new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(); }

        // --------------------------------------------------------------
        //  Action / follow-up timeline : trdinqry2actions -> followup (module='INQ')
        // --------------------------------------------------------------
        static class Followup
        {
            static Dictionary<string, InqTarget> _inqByKey;
            static Dictionary<int, string> _actToBe, _actTaken, _fixed;
            static Dictionary<string, int> _srcPersonCode = new Dictionary<string, int>();
            static Dictionary<string, int> _remarkByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            static Dictionary<string, int> _priorityMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            static int _cFollow, _cSkippedNoInq, _cErrors, _newRemarks;

            public static void Migrate(SqlConnection tgt, Dictionary<string, InqTarget> inqByKey)
            {
                Console.WriteLine("   -- Inquiry action/follow-ups (trdinqry2actions -> followup) --");
                _inqByKey = inqByKey;
                LoadSourceMasters();
                BuildPersonCodes(tgt);
                BuildFollowupRemarks(tgt);
                BuildPriorityMap(tgt);
                SeedInquiryTypes(tgt);

                Console.WriteLine("Maps: inquiries=" + _inqByKey.Count + ", persons=" + _srcPersonCode.Count + ", followupremarks=" + _remarkByName.Count + "(+" + _newRemarks + ")");
                Console.WriteLine("-----------------------------------------------------");

                string sql = @"
                    SELECT nofficeid, ninquiry, nactiontobetaken, dactiontobedate, npriority, nsalesman, nactiontaken,
                           dactiondate, vremarks, vactiontoberemarks, npartycontact, npartycontact1, addedon, editedon
                    FROM trdinqry2actions WITH (NOLOCK)
                    WHERE nofficeid IN (" + OfficeIn + @") AND ninquiry IS NOT NULL AND ninquiry > 0
                    ORDER BY nofficeid, ninquiry, ncode";

                using (SqlConnection src = OpenSrc())
                using (SqlCommand rc = new SqlCommand(sql, src))
                {
                    rc.CommandTimeout = 0;
                    using (SqlDataReader dr = rc.ExecuteReader())
                        while (dr.Read())
                        {
                            try { MigrateAction(tgt, dr); }
                            catch (Exception ex) { _cErrors++; if (_cErrors <= 20) Console.WriteLine("  [ERROR] action inq=" + Str(dr["ninquiry"]) + ": " + ex.Message); }
                        }
                }

                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("  Followups migrated       : " + _cFollow);
                Console.WriteLine("  Skipped (inquiry not found): " + _cSkippedNoInq);
                Console.WriteLine("  Errors                   : " + _cErrors);
                Console.WriteLine("-----------------------------------------------------");
            }

            static void MigrateAction(SqlConnection tgt, SqlDataReader dr)
            {
                int inq = Int(dr["ninquiry"]);
                int office = Int(dr["nofficeid"]);
                InqTarget t;
                if (!_inqByKey.TryGetValue(office + "|" + inq, out t)) { _cSkippedNoInq++; return; }
                int inqcode = t.Code;
                object ccode = t.Cs > 0 ? (object)t.Cs : DBNull.Value;
                string partyKey = t.Party ?? "";

                int doneCode = Int(dr["nactiontaken"]);
                int tobeCode = Int(dr["nactiontobetaken"]);
                string activity = doneCode > 0 ? LookInt(_actTaken, doneCode) : LookInt(_actToBe, tobeCode);
                int rc; object remarkscode = _remarkByName.TryGetValue(activity, out rc) ? (object)rc : DBNull.Value;
                string followType = MapType(activity);
                object purpose = PS(LookInt(_actToBe, tobeCode));

                int pc;
                object person = (partyKey.Length > 0 && Int(dr["npartycontact1"]) > 0
                                 && _srcPersonCode.TryGetValue(partyKey + "|" + Int(dr["npartycontact1"]), out pc))
                                ? (object)pc : DBNull.Value;

                int pcd; object priority = _priorityMap.TryGetValue(LookInt(_fixed, Int(dr["npriority"])), out pcd) ? (object)pcd : DBNull.Value;

                string remStr = Str(dr["vremarks"]);
                string vtr = Str(dr["vactiontoberemarks"]);
                // Add the Purpose Remark only when it differs from the Action Taken Remark (client often fills both
                // the same -> avoid showing the identical text twice). When they differ, keep both (unique data). - Aftab Alam
                if (vtr.Length > 0 && !string.Equals(vtr.Trim(), remStr.Trim(), StringComparison.OrdinalIgnoreCase))
                    remStr = (remStr.Length > 0 ? remStr + " | " : "") + "Purpose Remark: " + vtr;
                object remarks = PS(remStr);

                object lastf = P(dr["dactiondate"]);
                object nextf = P(dr["dactiontobedate"]);
                object createdon = P(dr["addedon"]);

                using (SqlCommand c = new SqlCommand(@"
                    INSERT INTO followup
                        (ccode, module, modulecode, followType, remarkscode, purpose, priority,
                         remarks, lastfollowup, nextfollowup, person, pcode, createdby, createdon)
                    VALUES (@ccode, 'INQ', @mc, @ftype, @rc, @purpose, @prio,
                            @remarks, @lastf, @nextf, @person, @person, @cb, @con)", tgt))
                {
                    int _sp;
                    c.Parameters.AddWithValue("@ccode", ccode);
                    c.Parameters.AddWithValue("@mc", inqcode);
                    c.Parameters.AddWithValue("@ftype", PS(followType));
                    c.Parameters.AddWithValue("@rc", remarkscode);
                    c.Parameters.AddWithValue("@purpose", purpose);
                    c.Parameters.AddWithValue("@prio", priority);
                    c.Parameters.AddWithValue("@remarks", remarks);
                    c.Parameters.AddWithValue("@lastf", lastf);
                    c.Parameters.AddWithValue("@nextf", nextf);
                    c.Parameters.AddWithValue("@person", person);
                    c.Parameters.AddWithValue("@cb", UserByCode.TryGetValue(NKey(dr["nsalesman"]), out _sp) ? (object)_sp : MigrationUser);
                    c.Parameters.AddWithValue("@con", createdon);
                    c.ExecuteNonQuery();
                }

                _cFollow++;
                if (_cFollow % 20000 == 0) Console.WriteLine("   ... " + _cFollow + " followups migrated");
            }

            static string MapType(string activity)
            {
                string s = (activity ?? "").ToLowerInvariant();
                if (s.Length == 0) return "Others";
                if (s.Contains("meeting") || s.Contains("meet ") || s.Contains("met for") || s.Contains("visit") ||
                    s.Contains("discussion") || s.Contains("demo") || s.Contains("presentation") ||
                    s.Contains("attend") || s.Contains("negotiation")) return "Meeting";
                if (s.Contains("call") || s.Contains("phone") || s.Contains("telephon")) return "Call";
                if (s.Contains("sms")) return "SMS";
                if (s.Contains("fax")) return "Fax";
                if (s.Contains("email") || s.Contains("e-mail") || s.Contains("mail") || s.Contains("send") ||
                    s.Contains("sent") || s.Contains("quotation") || s.Contains("quote") || s.Contains("brochure") ||
                    s.Contains("tender") || s.Contains("profile") || s.Contains("details") || s.Contains("letter") ||
                    s.Contains("certificate") || s.Contains("invoice") || s.Contains("questionnaire") ||
                    s.Contains("offer") || s.Contains("submit") || s.Contains("upload")) return "E-Mail";
                return "Others";
            }

            static void LoadSourceMasters()
            {
                _fixed = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
                _actToBe = SrcLookupInt("SELECT ncode, vname FROM mstactiontobetaken WITH (NOLOCK)");
                _actTaken = SrcLookupInt("SELECT ncode, vname FROM mstactiontaken WITH (NOLOCK)");
            }

            static void BuildPersonCodes(SqlConnection tgt)
            {
                Dictionary<string, int> mlt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow r in GetTgt("SELECT code, ccode, name FROM mltcontact WITH (NOLOCK)").Tables[0].Rows)
                {
                    int code = Int(r["code"]), cc = Int(r["ccode"]); string nm = Str(r["name"]);
                    if (code > 0 && cc > 0 && nm.Length > 0) { string k = cc + "|" + nm; if (!mlt.ContainsKey(k)) mlt[k] = code; }
                }

                _srcPersonCode = new Dictionary<string, int>();
                foreach (DataRow pr in GetSrc("SELECT nparty, ncode, ntitle, vcontactperson FROM msdparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND LTRIM(RTRIM(ISNULL(vcontactperson,''))) <> ''").Tables[0].Rows)
                {
                    string party = NKey(pr["nparty"]); if (party.Length == 0) continue;
                    int pn = Int(pr["ncode"]); if (pn <= 0) continue;
                    int cc;
                    if (!PartyToContact.TryGetValue(party, out cc)) continue;
                    string name = Cap(Join(LookInt(_fixed, Int(pr["ntitle"])), Str(pr["vcontactperson"])), 100);
                    int mcode;
                    if (mlt.TryGetValue(cc + "|" + name, out mcode))
                    {
                        string key = party + "|" + pn;
                        if (!_srcPersonCode.ContainsKey(key)) _srcPersonCode[key] = mcode;
                    }
                }
            }

            static void BuildFollowupRemarks(SqlConnection tgt)
            {
                _remarkByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow r in GetTgt("SELECT code, name FROM followupremarks WITH (NOLOCK)").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !_remarkByName.ContainsKey(n)) _remarkByName[n] = Int(r["code"]); }

                HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectNames("nactiontaken", _actTaken, names);
                CollectNames("nactiontobetaken", _actToBe, names);

                foreach (string nm in names)
                {
                    if (nm.Length == 0 || _remarkByName.ContainsKey(nm)) continue;
                    using (SqlCommand ins = new SqlCommand("INSERT INTO followupremarks (name, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES (@n, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", nm.Length > 500 ? nm.Substring(0, 500) : nm); ins.Parameters.AddWithValue("@cb", MigrationUser); int code = Convert.ToInt32(ins.ExecuteScalar()); _remarkByName[nm] = code; _newRemarks++; }
                }
                Console.WriteLine("  followupremarks (quick-remark master) seeded: " + _newRemarks);
            }

            static void CollectNames(string col, Dictionary<int, string> master, HashSet<string> names)
            {
                foreach (DataRow r in GetSrc("SELECT DISTINCT " + col + " AS v FROM trdinqry2actions WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND " + col + " IS NOT NULL AND " + col + " > 0").Tables[0].Rows)
                { string nm = LookInt(master, Int(r["v"])); if (nm.Length > 0) names.Add(nm); }
            }

            static void BuildPriorityMap(SqlConnection tgt)
            {
                _priorityMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow r in GetTgt("SELECT code, name FROM priority WITH (NOLOCK)").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !_priorityMap.ContainsKey(n)) _priorityMap[n] = Int(r["code"]); }
            }

            static void SeedInquiryTypes(SqlConnection tgt)
            {
                HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (SqlCommand c = new SqlCommand("SELECT name FROM miscellaneous WHERE module='Inquiry' AND type='Action Type'", tgt))
                using (SqlDataReader r = c.ExecuteReader())
                    while (r.Read()) { string n = Str(r["name"]); if (n.Length > 0) existing.Add(n); }

                int added = 0;
                foreach (DataRow r in GetSrc("SELECT DISTINCT nactionType FROM trhinqry WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND nactionType IS NOT NULL AND nactionType > 0").Tables[0].Rows)
                {
                    string nm = LookInt(_fixed, Int(r["nactionType"]));
                    if (nm.Length == 0 || existing.Contains(nm)) continue;
                    using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) VALUES ('Inquiry', 'Action Type', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); ins.ExecuteNonQuery(); }
                    existing.Add(nm); added++;
                }
                Console.WriteLine("  miscellaneous(Inquiry/Action Type) seeded: " + added);
            }
        }
    }

    // ==================================================================
    //  CUSTOMER QUOTATION : trhquote/trdquote1items -> inqcs (cors='CQ')/inqcsdet
    //  inqlink -> the migrated Customer Inquiry row. Structured: quotetype/status/category/lossreason/
    //  currency/exchangerate/enqrefno/enqrefdate/followupdate. Run AFTER Customer Inquiry.
    // ==================================================================
    static class CustomerQuotation
    {
        const string Cors = "CQ";

        static Dictionary<int, string> _fixed, _lostReason, _inqCategory, _officeName;
        static readonly Dictionary<int, int> _mapStatus = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _mapCategory = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _mapLostReason = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _mapQuoteType = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _currencyMap = new Dictionary<int, int>();
        static readonly Dictionary<string, int> _custByParty = new Dictionary<string, int>();
        static readonly Dictionary<int, int> _branchByContact = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _prodByItem = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _branchByOffice = new Dictionary<int, int>();
        static readonly Dictionary<string, string> _persons = new Dictionary<string, string>();
        static readonly Dictionary<string, List<decimal[]>> _hdrProducts = new Dictionary<string, List<decimal[]>>();
        static readonly Dictionary<string, string> _srcInqRef = new Dictionary<string, string>();
        static readonly Dictionary<string, int> _ciByKey = new Dictionary<string, int>();
        static readonly Dictionary<string, decimal> _itemsAllByKey = new Dictionary<string, decimal>();   // office|quote -> sum(qty x nrate) of ALL lines
        static readonly Dictionary<string, decimal> _lineTaxByKey = new Dictionary<string, decimal>();    // office|quote -> sum(line ntaxamt)
        static Dictionary<string, List<object[]>> _chargesByKey;
        static Dictionary<int, string> _taxsetName;
        static int _cRows, _cDet, _cDetSkipped, _cErrors, _custResolved, _custUnresolved, _newStatus, _newCategory, _newLostReason, _newQuoteType, _inqLinked;
        static int _cAdj, _docGst, _lineDisc;

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: Customer Quotation (inqcs cors='" + Cors + "')");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            LoadSourceMasters();

            using (SqlConnection tgt = OpenTgt())
            {
                EnsureStatuses(tgt);
                SeedCategories(tgt);
                SeedLostReasons(tgt);
                SeedQuoteTypes(tgt);
                BuildCurrencyMap(tgt);
                BuildCustomerMap(tgt);
                BuildBranchByContact(tgt);
                EnsureUserByCode();
                BuildProductMap(tgt);
                LoadPersons(tgt);
                LoadHeaderProducts();
                _chargesByKey = LoadPostTaxCharges("trdquote3posttaxchgs", "nquote");
                _taxsetName = SrcLookupInt("SELECT ncode, vname FROM msttaxset WITH (NOLOCK)");
                EnsureBranches(tgt);
                BuildInqLink(tgt);

                Console.WriteLine("Maps: status=" + _mapStatus.Count + "(+" + _newStatus + "), category=" + _mapCategory.Count + "(+" + _newCategory +
                                  "), lostReason=" + _mapLostReason.Count + "(+" + _newLostReason + "), quoteType=" + _mapQuoteType.Count + "(+" + _newQuoteType +
                                  "), currency=" + _currencyMap.Count + ", customers=" + _custByParty.Count + ", products=" + _prodByItem.Count +
                                  ", persons=" + _persons.Count + ", hdrWithProducts=" + _hdrProducts.Count + ", ciRows=" + _ciByKey.Count);
                Console.WriteLine("-----------------------------------------------------");

                string headerSql = @"
                    SELECT ncode, vtrnprefix, ntrnno, dtrndate, nparty, npartycontact, nsalesman, nquotetype, ninquiry, nquotestatus,
                           ninquirycategory, vvalidity, dfollowupdate, nlostreasons, ncurrency, nexchangerate, vrefno, drefdate,
                           vremarks, vcomment, nofficeid, addedon, editedon
                    FROM trhquote WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY ncode";

                using (SqlConnection src = OpenSrc())
                using (SqlCommand rc = new SqlCommand(headerSql, src))
                {
                    rc.CommandTimeout = 0;
                    using (SqlDataReader dr = rc.ExecuteReader())
                        while (dr.Read())
                        {
                            try { InsertRow(tgt, Map(dr)); }
                            catch (Exception ex) { _cErrors++; if (_cErrors <= 20) Console.WriteLine("  [ERROR] " + ex.Message); }
                        }
                }

                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("  Customer Quotation migrated : " + _cRows);
                Console.WriteLine("  inqcsdet lines   : " + _cDet + "  (skipped " + _cDetSkipped + ")");
                Console.WriteLine("  Customer cscode  : " + _custResolved + " resolved, " + _custUnresolved + " unresolved");
                Console.WriteLine("  Inquiry linked   : " + _inqLinked + " (inqlink -> migrated CI row)");
                Console.WriteLine("  Tax/charges      : quotations with GST % " + _docGst + ", inq_adjust rows " + _cAdj + ", lines with discount " + _lineDisc);
                Console.WriteLine("  Masters added    : status " + _newStatus + ", category " + _newCategory + ", lostReason " + _newLostReason + ", quoteType " + _newQuoteType);
                Console.WriteLine("  Errors           : " + _cErrors);
                Console.WriteLine("-----------------------------------------------------");
            }
        }

        static InqRow Map(SqlDataReader dr)
        {
            int office = Int(dr["nofficeid"]);
            int ninquiry = Int(dr["ninquiry"]);

            object inqlink = DBNull.Value;
            string iref; int br, cicode;
            if (ninquiry > 0 && _srcInqRef.TryGetValue(office + "|" + ninquiry, out iref) && iref.Length > 0
                && _branchByOffice.TryGetValue(office, out br) && _ciByKey.TryGetValue(br + "|" + iref, out cicode))
            { inqlink = cicode; _inqLinked++; }

            InqRow m = new InqRow();
            m.SourceCode = Int(dr["ncode"]);
            m.Inqref = (Str(dr["vtrnprefix"]) + Str(dr["ntrnno"])).Trim();
            m.Inqdate = P(dr["dtrndate"]);
            m.FollowupDate = P(dr["dfollowupdate"]);
            m.PartyKey = NKey(dr["nparty"]);
            m.PersonNo = Int(dr["npartycontact"]);
            m.StatusCode = Int(dr["nquotestatus"]);
            m.CategoryCode = Int(dr["ninquirycategory"]);
            m.Office = office;
            m.InqLink = inqlink;
            int qt; m.QuoteType = _mapQuoteType.TryGetValue(Int(dr["nquotetype"]), out qt) ? (object)qt : DBNull.Value;
            int lr; m.LossReason = _mapLostReason.TryGetValue(Int(dr["nlostreasons"]), out lr) ? (object)lr : DBNull.Value;
            int cu; m.Currency = _currencyMap.TryGetValue(Int(dr["ncurrency"]), out cu) ? (object)cu : DBNull.Value;
            int ex; m.Executive = UserByCode.TryGetValue(NKey(dr["nsalesman"]), out ex) ? (object)ex : MigrationUser;
            m.ExchangeRate = dr["nexchangerate"] != DBNull.Value ? dr["nexchangerate"] : DBNull.Value;
            m.EnqRefNo = Cap(Str(dr["vrefno"]), 200);
            m.EnqRefDate = P(dr["drefdate"]);
            m.Createdon = P(dr["addedon"]);
            m.Updatedon = dr["editedon"] != DBNull.Value ? dr["editedon"] : P(dr["addedon"]);
            m.Remark = BuildRemark(dr);
            return m;
        }

        static string BuildRemark(SqlDataReader dr)
        {
            List<string> parts = new List<string>();
            string rem = Str(dr["vremarks"]); if (!string.IsNullOrWhiteSpace(rem)) parts.Add(rem);
            string cm = Str(dr["vcomment"]); if (!string.IsNullOrWhiteSpace(cm)) parts.Add("Comment: " + cm);
            string vv = Str(dr["vvalidity"]); if (!string.IsNullOrWhiteSpace(vv)) parts.Add("Validity: " + vv);
            return string.Join(" | ", parts);
        }

        static void InsertRow(SqlConnection tgt, InqRow m)
        {
            object cscode = DBNull.Value, csbranch = DBNull.Value;
            int cid, br;
            if (m.PartyKey.Length > 0 && _custByParty.TryGetValue(m.PartyKey, out cid))
            {
                cscode = cid; _custResolved++;
                if (_branchByContact.TryGetValue(cid, out br)) csbranch = br;
            }
            else if (m.PartyKey.Length > 0) _custUnresolved++;

            string cperson = "";
            if (m.PersonNo > 0 && m.PartyKey.Length > 0) _persons.TryGetValue(m.PartyKey + "|" + m.PersonNo, out cperson);

            int st; object status = _mapStatus.TryGetValue(m.StatusCode, out st) ? (object)st : DBNull.Value;
            int ca; object category = _mapCategory.TryGetValue(m.CategoryCode, out ca) ? (object)ca : DBNull.Value;
            int b; object branch = _branchByOffice.TryGetValue(m.Office, out b) ? (object)b : DBNull.Value;

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                int detN = 0, detSkip = 0;
                try
                {
                    int code;
                    string ins = @"
                        INSERT INTO inqcs
                            (cors, inqref, inqdate, followupdate, cscode, csbranch, cperson, executive, status, Category,
                             quotetype, lossreason, currency, exchangerate, inqlink, enqrefno, enqrefdate,
                             comcode, branchcode, remark, createdby, updatedby, createdon, updatedon)
                        OUTPUT INSERTED.code
                        VALUES (@cors, @inqref, @inqdate, @followupdate, @cscode, @csbranch, @cperson, @exec, @status, @cat,
                                @quotetype, @lossreason, @currency, @exrate, @inqlink, @enqrefno, @enqrefdate,
                                @com, @branch, @remark, @cb, @cb, @con, @uon)";
                    using (SqlCommand c = new SqlCommand(ins, tgt, tx))
                    {
                        c.Parameters.AddWithValue("@cors", Cors);
                        c.Parameters.AddWithValue("@inqref", PS(m.Inqref));
                        c.Parameters.AddWithValue("@inqdate", m.Inqdate);
                        c.Parameters.AddWithValue("@followupdate", m.FollowupDate);
                        c.Parameters.AddWithValue("@cscode", cscode);
                        c.Parameters.AddWithValue("@csbranch", csbranch);
                        c.Parameters.AddWithValue("@cperson", PS(cperson));
                        c.Parameters.AddWithValue("@exec", m.Executive);
                        c.Parameters.AddWithValue("@status", status);
                        c.Parameters.AddWithValue("@cat", category);
                        c.Parameters.AddWithValue("@quotetype", m.QuoteType);
                        c.Parameters.AddWithValue("@lossreason", m.LossReason);
                        c.Parameters.AddWithValue("@currency", m.Currency);
                        c.Parameters.AddWithValue("@exrate", m.ExchangeRate);
                        c.Parameters.AddWithValue("@inqlink", m.InqLink);
                        c.Parameters.AddWithValue("@enqrefno", PS(m.EnqRefNo));
                        c.Parameters.AddWithValue("@enqrefdate", m.EnqRefDate);
                        c.Parameters.AddWithValue("@com", CompanyCode);
                        c.Parameters.AddWithValue("@branch", branch);
                        c.Parameters.AddWithValue("@remark", PS(m.Remark));
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        c.Parameters.AddWithValue("@con", m.Createdon);
                        c.Parameters.AddWithValue("@uon", m.Updatedon);
                        code = Convert.ToInt32(c.ExecuteScalar());
                    }

                    string dkey = m.Office + "|" + m.SourceCode;
                    decimal itemsAll, lineTax, gstRate; List<object[]> chg;
                    _itemsAllByKey.TryGetValue(dkey, out itemsAll); _lineTaxByKey.TryGetValue(dkey, out lineTax); _chargesByKey.TryGetValue(dkey, out chg);
                    List<object[]> adj = ComputeCharges(itemsAll, chg, out gstRate);
                    decimal migratedValue = 0m; int discN = 0;

                    List<decimal[]> plist;
                    if (_hdrProducts.TryGetValue(dkey, out plist))
                    {
                        foreach (decimal[] p in plist)
                        {
                            int pcode;
                            if (!_prodByItem.TryGetValue((int)p[0], out pcode)) { detSkip++; continue; }
                            int lc; object lineCur = _currencyMap.TryGetValue((int)p[4], out lc) ? (object)lc : DBNull.Value;
                            // eBizWiz nrate is already net of ndiscountperc -> price = master rate, discount = per-unit (master - nrate),
                            // so qty x (price - discount) = qty x nrate and a form edit keeps the same net. - Aftab Alam
                            object price, disc = DBNull.Value, dpct = DBNull.Value;
                            if (p[3] != 0 && p[5] > p[2]) { price = p[5]; disc = p[5] - p[2]; dpct = p[3]; discN++; }
                            else price = p[2] == 0 ? (object)DBNull.Value : p[2];
                            decimal lr = 0m; string tsn;
                            if (gstRate > 0) lr = gstRate;
                            else if ((int)p[6] > 0 && p[7] != 0 && _taxsetName.TryGetValue((int)p[6], out tsn)) lr = PercentIn(tsn);
                            using (SqlCommand d = new SqlCommand(@"INSERT INTO inqcsdet
                                                       (inqcode, pcode, quantity, price, discount, discountpercent, prodtax, currency, createdby, updatedby, createdon, updatedon)
                                                   VALUES (@i, @p, @q, @pr, @disc, @dp, @tax, @cur, @cb, @cb, @con, @uon)", tgt, tx))
                            {
                                d.Parameters.AddWithValue("@i", code);
                                d.Parameters.AddWithValue("@p", pcode);
                                d.Parameters.AddWithValue("@q", p[1]);
                                d.Parameters.AddWithValue("@pr", price);
                                d.Parameters.AddWithValue("@disc", disc);
                                d.Parameters.AddWithValue("@dp", dpct);
                                d.Parameters.AddWithValue("@tax", lr > 0 ? (object)lr : DBNull.Value);
                                d.Parameters.AddWithValue("@cur", lineCur);
                                d.Parameters.AddWithValue("@cb", MigrationUser);
                                d.Parameters.AddWithValue("@con", m.Createdon);
                                d.Parameters.AddWithValue("@uon", m.Updatedon);
                                d.ExecuteNonQuery();
                            }
                            migratedValue += p[1] * p[2];
                            detN++;
                        }
                    }

                    // Totals = eBizWiz: blank-item/unmapped lines and line Tax Set amounts can't sit on a migrated line -> named adjustments.
                    decimal otherItems = Math.Round(itemsAll - migratedValue, 2);
                    if (otherItems != 0) adj.Insert(0, new object[] { "Other items (eBizWiz)", 0m, otherItems });
                    if (lineTax != 0) adj.Add(new object[] { "Tax on items (eBizWiz)", 0m, lineTax });
                    int adjN = InsertInqAdjustments(tgt, tx, code, adj, m.Createdon, m.Updatedon);

                    tx.Commit();
                    _cRows++; _cDet += detN; _cDetSkipped += detSkip;
                    _cAdj += adjN; _lineDisc += discN; if (gstRate > 0) _docGst++;
                    if (_cRows % 2000 == 0) Console.WriteLine("  ... " + _cRows + " Customer Quotation migrated");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        static void LoadSourceMasters()
        {
            _fixed = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _inqCategory = SrcLookupInt("SELECT ncode, vname FROM mstinquirycategory WITH (NOLOCK)");
            _lostReason = SrcLookupInt("SELECT ncode, vname FROM mstorderlostreason WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
        }

        static void EnsureStatuses(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // CQ status dropdown reads status where module='Quotation' (app/inquiry.asp) — match/seed under 'Quotation', not 'Inquiry'. - Aftab Alam
            foreach (DataRow r in GetTgt("SELECT code, name FROM status WHERE module='Quotation'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (DataRow row in GetSrc("SELECT DISTINCT nquotestatus FROM trhquote WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND nquotestatus IS NOT NULL").Tables[0].Rows)
            {
                int scode = Int(row[0]);
                string nm = LookInt(_fixed, scode);
                if (nm.Length == 0) continue;
                int tc;
                if (!tmap.TryGetValue(nm, out tc))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO status (name, module, sort, makedefault, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, 'Quotation', 0, 0, @cb, @cb, GETDATE(), GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); tc = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[nm] = tc; _newStatus++;
                }
                _mapStatus[scode] = tc;
            }
        }

        static void SeedCategories(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = LoadMisc(tgt, "Category");
            foreach (KeyValuePair<int, string> kv in _inqCategory)
            {
                string n = (kv.Value ?? "").Trim(); if (n.Length == 0) continue;
                int code;
                if (!tmap.TryGetValue(n, out code)) { code = InsertMisc(tgt, "Category", n); tmap[n] = code; _newCategory++; }
                _mapCategory[kv.Key] = code;
            }
        }

        static void SeedLostReasons(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = LoadMisc(tgt, "Loss Reason");
            foreach (int scode in DistinctSrcCodes("nlostreasons"))
            {
                string n = LookInt(_lostReason, scode).Trim();
                if (n.Length == 0) continue;
                int code;
                if (!tmap.TryGetValue(n, out code)) { code = InsertMisc(tgt, "Loss Reason", n); tmap[n] = code; _newLostReason++; }
                _mapLostReason[scode] = code;
            }
        }

        static void SeedQuoteTypes(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = LoadMisc(tgt, "Quote Type");
            // Seed the FULL fixedselection nquotetype group so the form shows all client Quote Types (not only used). - Aftab Alam
            foreach (DataRow row in GetSrc("SELECT DISTINCT vdisplayvalue AS v FROM mstfixedselection WITH (NOLOCK) WHERE vfieldname='nquotetype'").Tables[0].Rows)
            {
                string n = Str(row["v"]).Trim();
                if (n.Length == 0) continue;
                if (!tmap.ContainsKey(n)) { tmap[n] = InsertMisc(tgt, "Quote Type", n); _newQuoteType++; }
            }
            // Map the used source codes -> their seeded miscellaneous code (by name).
            foreach (int scode in DistinctSrcCodes("nquotetype"))
            {
                string n = LookInt(_fixed, scode).Trim();
                if (n.Length == 0) continue;
                int code;
                if (tmap.TryGetValue(n, out code)) _mapQuoteType[scode] = code;
            }
        }

        static void BuildCurrencyMap(SqlConnection tgt)
        {
            Dictionary<string, int> t = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name, sname FROM currency").Tables[0].Rows)
            {
                int code = Int(r["code"]); string nm = Str(r["name"]).Trim(), sn = Str(r["sname"]).Trim();
                if (sn.Length > 0 && !t.ContainsKey(sn)) t[sn] = code;
                if (nm.Length > 0 && !t.ContainsKey(nm)) t[nm] = code;
            }
            foreach (DataRow r in GetSrc("SELECT ncode, vmajordenomination, vmajorshortname FROM mstcurrency WITH (NOLOCK)").Tables[0].Rows)
            {
                int sc = Int(r["ncode"]); string nm = Str(r["vmajordenomination"]).Trim(), sn = Str(r["vmajorshortname"]).Trim();
                int a;
                if (sn.Length > 0 && t.TryGetValue(sn, out a)) _currencyMap[sc] = a;
                else if (nm.Length > 0 && t.TryGetValue(nm, out a)) _currencyMap[sc] = a;
            }
        }

        static void BuildCustomerMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM contact WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }

            foreach (DataRow r in GetSrc("SELECT ncode, ntitle, vname FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = NKey(r["ncode"]); if (key.Length == 0) continue;
                string nm = Cap(Join(LookInt(_fixed, Int(r["ntitle"])), Str(r["vname"])), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_custByParty.ContainsKey(key)) _custByParty[key] = code;
            }
            foreach (KeyValuePair<string, int> kv in PartyToContact) _custByParty[kv.Key] = kv.Value;
        }

        static void BuildBranchByContact(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, ccode, isnull(isdefault,0) as isdefault FROM mltaddress WITH (NOLOCK) ORDER BY ccode, isdefault DESC, code").Tables[0].Rows)
            {
                int ccode = Int(r["ccode"]); if (ccode <= 0) continue;
                if (!_branchByContact.ContainsKey(ccode)) _branchByContact[ccode] = Int(r["code"]);
            }
        }

        static void BuildProductMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM product WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }

            foreach (DataRow r in GetSrc("SELECT ncode, vname FROM mstitems WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                int ncode = Int(r["ncode"]);
                string nm = Cap(Str(r["vname"]), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_prodByItem.ContainsKey(ncode)) _prodByItem[ncode] = code;
            }
        }

        static void LoadPersons(SqlConnection tgt)
        {
            Dictionary<string, int> mltMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, ccode, name FROM mltcontact WITH (NOLOCK)").Tables[0].Rows)
            {
                int code = Int(r["code"]), ccode = Int(r["ccode"]); string name = Str(r["name"]);
                if (code > 0 && ccode > 0 && name.Length > 0) { string k = ccode + "|" + name; if (!mltMap.ContainsKey(k)) mltMap[k] = code; }
            }

            foreach (DataRow dr in GetSrc("SELECT nparty, ncode, ntitle, vcontactperson FROM msdparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND LTRIM(RTRIM(ISNULL(vcontactperson,''))) <> ''").Tables[0].Rows)
            {
                string partyKey = NKey(dr["nparty"]); if (partyKey.Length == 0) continue;
                int personNo = Int(dr["ncode"]); if (personNo <= 0) continue;
                string key = partyKey + "|" + personNo;
                if (_persons.ContainsKey(key)) continue;

                string rawName = Str(dr["vcontactperson"]);
                string formattedName = Cap(Join(LookInt(_fixed, Int(dr["ntitle"])), rawName), 150);
                string mltCodeStr = "";
                int ccode2, mcode, mcode2;
                if (_custByParty.TryGetValue(partyKey, out ccode2))
                {
                    if (mltMap.TryGetValue(ccode2 + "|" + formattedName, out mcode)) mltCodeStr = mcode.ToString();
                    else if (mltMap.TryGetValue(ccode2 + "|" + rawName, out mcode2)) mltCodeStr = mcode2.ToString();
                }
                _persons[key] = mltCodeStr;
            }
        }

        static void LoadHeaderProducts()
        {
            // ALL lines feed the item total + line tax (eBizWiz total); only item lines become inqcsdet.
            // decimal[] = { item, qty, nrate, ndiscountperc, currency, nmasterrate, ntaxset, ntaxamt }
            foreach (DataRow r in GetSrc("SELECT nofficeid, nquote, nitem, nquantity, nrate, ndiscountperc, nitemcurrency, nmasterrate, ntaxset, ntaxamt FROM trdquote1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, nquote, ncode").Tables[0].Rows)
            {
                int office = Int(r[0]);
                int hdr = Int(r[1]); if (hdr <= 0) continue;
                string key = office + "|" + hdr;
                decimal cur;
                _itemsAllByKey.TryGetValue(key, out cur); _itemsAllByKey[key] = cur + Dec(r[3]) * Dec(r[4]);
                _lineTaxByKey.TryGetValue(key, out cur); _lineTaxByKey[key] = cur + Dec(r[9]);
                int item = Int(r[2]); if (item <= 0) continue;
                List<decimal[]> list;
                if (!_hdrProducts.TryGetValue(key, out list)) { list = new List<decimal[]>(); _hdrProducts[key] = list; }
                list.Add(new decimal[] { item, Dec(r[3]), Dec(r[4]), Dec(r[5]), Int(r[6]), Dec(r[7]), Int(r[8]), Dec(r[9]) });
            }
        }

        static void EnsureBranches(SqlConnection tgt)
        {
            foreach (int office in Offices)
            {
                string nm;
                string place = _officeName.TryGetValue(office, out nm) && nm.Length > 0 ? (nm.Length > 100 ? nm.Substring(0, 100) : nm) : "Office " + office;
                using (SqlCommand chk = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                {
                    chk.Parameters.AddWithValue("@cc", CompanyCode);
                    chk.Parameters.AddWithValue("@pl", place);
                    object o = chk.ExecuteScalar();
                    if (o != null && o != DBNull.Value) _branchByOffice[office] = Convert.ToInt32(o);
                }
            }
        }

        static void BuildInqLink(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, branchcode, inqref FROM inqcs WITH (NOLOCK) WHERE cors='CI' AND branchcode IS NOT NULL AND inqref IS NOT NULL").Tables[0].Rows)
            {
                string key = Int(r["branchcode"]) + "|" + Str(r["inqref"]).Trim();
                if (!_ciByKey.ContainsKey(key)) _ciByKey[key] = Int(r["code"]);
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhinqry WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = Int(r["nofficeid"]) + "|" + Int(r["ncode"]);
                if (!_srcInqRef.ContainsKey(key)) _srcInqRef[key] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            }
        }

        static Dictionary<string, int> LoadMisc(SqlConnection tgt, string type)
        {
            Dictionary<string, int> m = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand c = new SqlCommand("SELECT code, name FROM miscellaneous WHERE module='Inquiry' AND type=@t", tgt))
            {
                c.Parameters.AddWithValue("@t", type);
                using (SqlDataReader r = c.ExecuteReader()) while (r.Read()) { string n = Str(r["name"]); if (n.Length > 0 && !m.ContainsKey(n)) m[n] = Int(r["code"]); }
            }
            return m;
        }

        static int InsertMisc(SqlConnection tgt, string type, string name)
        {
            using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Inquiry', @t, @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
            {
                ins.Parameters.AddWithValue("@t", type);
                ins.Parameters.AddWithValue("@n", name);
                ins.Parameters.AddWithValue("@cb", MigrationUser);
                return Convert.ToInt32(ins.ExecuteScalar());
            }
        }

        static List<int> DistinctSrcCodes(string col)
        {
            List<int> codes = new List<int>();
            foreach (DataRow r in GetSrc("SELECT DISTINCT " + col + " FROM trhquote WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND " + col + " IS NOT NULL").Tables[0].Rows)
                codes.Add(Int(r[0]));
            return codes;
        }

        static string Join(string a, string b) { return string.Join(" ", new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(); }

        class InqRow
        {
            public int SourceCode;
            public string Inqref = "";
            public object Inqdate = DBNull.Value, FollowupDate = DBNull.Value, Createdon = DBNull.Value, Updatedon = DBNull.Value;
            public string PartyKey = "";
            public int PersonNo, StatusCode, CategoryCode, Office;
            public object Executive = MigrationUser, InqLink = DBNull.Value, QuoteType = DBNull.Value, LossReason = DBNull.Value,
                          Currency = DBNull.Value, ExchangeRate = DBNull.Value, EnqRefDate = DBNull.Value;
            public string EnqRefNo = "", Remark = "";
        }
    }

    // ==================================================================
    //  SALES ORDER : trhordrc/trdordrc1items -> inqcs (cors='SO')/inqcsdet
    //  inqlink -> migrated Quotation; inqcode -> migrated Inquiry. Bill/Ship To -> address text.
    //  Order check list (trdordrc5checks) -> taskchecklist (module='SO',
    //  modulecode = Sales Order inqcs.code; no task record created). Run AFTER Customer Quotation.
    // ==================================================================
    static class SalesOrder
    {
        const string Cors = "SO";

        static Dictionary<int, string> _fixed, _inqCategory, _officeName;
        static readonly Dictionary<int, int> _mapStatus = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _mapCategory = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _currencyMap = new Dictionary<int, int>();
        static readonly Dictionary<string, int> _custByParty = new Dictionary<string, int>();
        static readonly Dictionary<int, int> _branchByContact = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _prodByItem = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _branchByOffice = new Dictionary<int, int>();
        static readonly Dictionary<string, string> _persons = new Dictionary<string, string>();
        static readonly Dictionary<string, List<decimal[]>> _hdrProductsByKey = new Dictionary<string, List<decimal[]>>();
        static readonly Dictionary<string, string> _srcQuoteRef = new Dictionary<string, string>();
        static readonly Dictionary<string, int> _cqByKey = new Dictionary<string, int>();
        static readonly Dictionary<string, string> _srcInqRef = new Dictionary<string, string>();
        static readonly Dictionary<string, int> _ciByKey = new Dictionary<string, int>();
        static readonly Dictionary<string, string> _partyBillAddr = new Dictionary<string, string>();
        static readonly Dictionary<string, string> _partyInstAddr = new Dictionary<string, string>();
        static readonly Dictionary<string, List<object[]>> _checklistsByKey = new Dictionary<string, List<object[]>>();
        static readonly Dictionary<string, string> _checkName = new Dictionary<string, string>();
        static int _cRows, _cDet, _cDetSkipped, _cErrors, _custResolved, _custUnresolved, _newStatus, _newCategory, _quoteLinked, _cChecklist;
        static readonly Dictionary<string, decimal> _itemsAllByKey = new Dictionary<string, decimal>();   // office|order -> sum(qty x nrate) of ALL lines
        static readonly Dictionary<string, decimal> _lineTaxByKey = new Dictionary<string, decimal>();    // office|order -> sum(line ntaxamt)
        static Dictionary<string, List<object[]>> _chargesByKey;
        static Dictionary<int, string> _taxsetName;
        static int _cAdj, _docGst, _lineDisc;

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: Sales Order (inqcs cors='" + Cors + "')");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            LoadSourceMasters();

            using (SqlConnection tgt = OpenTgt())
            {
                EnsureStatuses(tgt);
                SeedCategories(tgt);
                SeedOrderType(tgt);
                BuildCurrencyMap(tgt);
                BuildCustomerMap(tgt);
                BuildBranchByContact(tgt);
                EnsureUserByCode();
                BuildProductMap(tgt);
                LoadPersons(tgt);
                LoadHeaderProducts();
                _chargesByKey = LoadPostTaxCharges("trdordrc4posttaxchgs", "nordrc");
                _taxsetName = SrcLookupInt("SELECT ncode, vname FROM msttaxset WITH (NOLOCK)");
                EnsureBranches(tgt);
                BuildInqLink(tgt);
                BuildPartyAddresses();
                LoadChecklists();

                Console.WriteLine("Maps: status=" + _mapStatus.Count + "(+" + _newStatus + "), category=" + _mapCategory.Count + "(+" + _newCategory +
                                  "), currency=" + _currencyMap.Count + ", customers=" + _custByParty.Count + ", products=" + _prodByItem.Count +
                                  ", persons=" + _persons.Count + ", hdrWithProducts=" + _hdrProductsByKey.Count + ", cqRows=" + _cqByKey.Count);
                Console.WriteLine("-----------------------------------------------------");

                string headerSql = @"
                    SELECT ncode, vtrnprefix, ntrnno, dtrndate, nfromparty, npartycontact, npartycontact2, nordrecdtype, nordsource,
                           nordrecdstatus, ninquirycategory, nterms, nquote, ninquiry, vrefno, drefdate,
                           nbillto, nshipto, nsalesman, ncurrency, nexchangerate, vremarks, vcomment, npendingreason,
                           vordacknno, dackndate, nofficeid, addedon, editedon
                    FROM trhordrc WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY ncode";

                using (SqlConnection src = OpenSrc())
                using (SqlCommand rc = new SqlCommand(headerSql, src))
                {
                    rc.CommandTimeout = 0;
                    using (SqlDataReader dr = rc.ExecuteReader())
                        while (dr.Read())
                        {
                            try { InsertRow(tgt, Map(dr)); }
                            catch (Exception ex) { _cErrors++; if (_cErrors <= 20) Console.WriteLine("  [ERROR] " + ex.Message); }
                        }
                }

                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("  Sales Order migrated  : " + _cRows);
                Console.WriteLine("  inqcsdet lines   : " + _cDet + "  (skipped " + _cDetSkipped + ")");
                Console.WriteLine("  Customer cscode  : " + _custResolved + " resolved, " + _custUnresolved + " unresolved");
                Console.WriteLine("  Quotation linked : " + _quoteLinked + " (inqlink -> migrated Quotation)");
                Console.WriteLine("  Tax/charges      : orders with GST % " + _docGst + ", inq_adjust rows " + _cAdj + ", lines with discount " + _lineDisc);
                Console.WriteLine("  Masters added    : status " + _newStatus + ", category " + _newCategory);
                Console.WriteLine("  Check list items : " + _cChecklist + "  (taskchecklist, module='SO')");
                Console.WriteLine("  Errors           : " + _cErrors);
                Console.WriteLine("-----------------------------------------------------");
            }
        }

        static InqRow Map(SqlDataReader dr)
        {
            int office = Int(dr["nofficeid"]);
            int quoteNc = Int(dr["nquote"]);
            int inqNc = Int(dr["ninquiry"]);

            object inqlink = DBNull.Value;
            string qref; int brq, cqcode;
            if (quoteNc > 0 && _srcQuoteRef.TryGetValue(office + "|" + quoteNc, out qref) && qref.Length > 0
                && _branchByOffice.TryGetValue(office, out brq) && _cqByKey.TryGetValue(brq + "|" + qref, out cqcode))
            { inqlink = cqcode; _quoteLinked++; }

            object inqcode = DBNull.Value;
            string iref; int bri, cicode;
            if (inqNc > 0 && _srcInqRef.TryGetValue(office + "|" + inqNc, out iref) && iref.Length > 0
                && _branchByOffice.TryGetValue(office, out bri) && _ciByKey.TryGetValue(bri + "|" + iref, out cicode))
            { inqcode = cicode; }

            // Ship To = branch reference (mltaddress code). Bill To = csbranch (customer branch, set above). - Aftab Alam
            object shipBranch = ResolveBranch(NKey(dr["nshipto"]));

            InqRow m = new InqRow();
            m.SourceCode = Int(dr["ncode"]);
            m.Office = office;
            m.Inqref = (Str(dr["vtrnprefix"]) + Str(dr["ntrnno"])).Trim();
            m.Inqdate = P(dr["dtrndate"]);
            m.PartyKey = NKey(dr["nfromparty"]);
            m.PersonNo = Int(dr["npartycontact"]);
            m.PersonNo2 = Int(dr["npartycontact2"]);
            m.StatusCode = Int(dr["nordrecdstatus"]);
            m.CategoryCode = Int(dr["ninquirycategory"]);
            m.OrderType = LookInt(_fixed, Int(dr["nordrecdtype"]));
            m.Sources = LookInt(_fixed, Int(dr["nordsource"]));
            int cu; m.Currency = _currencyMap.TryGetValue(Int(dr["ncurrency"]), out cu) ? (object)cu : DBNull.Value;
            int ex; m.Executive = UserByCode.TryGetValue(NKey(dr["nsalesman"]), out ex) ? (object)ex : MigrationUser;
            m.ExchangeRate = dr["nexchangerate"] != DBNull.Value ? dr["nexchangerate"] : DBNull.Value;
            m.Ponumber = Cap(Str(dr["vrefno"]), 200);
            m.Podate = P(dr["drefdate"]);
            m.ShippingBranch = shipBranch;
            m.InqLink = inqlink;
            m.InqCode = inqcode;
            m.Createdon = P(dr["addedon"]);
            m.Updatedon = dr["editedon"] != DBNull.Value ? dr["editedon"] : P(dr["addedon"]);
            m.Remark = BuildRemark(dr);
            return m;
        }

        static string BuildRemark(SqlDataReader dr)
        {
            List<string> parts = new List<string>();
            string rem = Str(dr["vremarks"]); if (!string.IsNullOrWhiteSpace(rem)) parts.Add(rem);
            string cm = Str(dr["vcomment"]); if (!string.IsNullOrWhiteSpace(cm)) parts.Add("Comment: " + cm);
            string pr = LookInt(_fixed, Int(dr["npendingreason"])); if (!string.IsNullOrWhiteSpace(pr)) parts.Add("Pending Reason: " + pr);
            string an = Str(dr["vordacknno"]); if (!string.IsNullOrWhiteSpace(an)) parts.Add("Order Ackn No: " + an);
            if (dr["dackndate"] != DBNull.Value) parts.Add("Order Ackn Date: " + Convert.ToDateTime(dr["dackndate"]).ToString("dd/MM/yyyy"));
            return string.Join(" | ", parts);
        }

        static void InsertRow(SqlConnection tgt, InqRow m)
        {
            object cscode = DBNull.Value, csbranch = DBNull.Value;
            int cid, br;
            if (m.PartyKey.Length > 0 && _custByParty.TryGetValue(m.PartyKey, out cid))
            {
                cscode = cid; _custResolved++;
                if (_branchByContact.TryGetValue(cid, out br)) csbranch = br;
            }
            else if (m.PartyKey.Length > 0) _custUnresolved++;

            // Party Contact 1 + 2 -> cperson, comma-separated (both contacts, per senior).
            string cperson = "";
            if (m.PartyKey.Length > 0)
            {
                List<string> pl = new List<string>();
                string p1, p2;
                if (m.PersonNo > 0 && _persons.TryGetValue(m.PartyKey + "|" + m.PersonNo, out p1) && p1.Length > 0) pl.Add(p1);
                if (m.PersonNo2 > 0 && _persons.TryGetValue(m.PartyKey + "|" + m.PersonNo2, out p2) && p2.Length > 0 && !pl.Contains(p2)) pl.Add(p2);
                cperson = string.Join(",", pl);
            }

            int st; object status = _mapStatus.TryGetValue(m.StatusCode, out st) ? (object)st : DBNull.Value;
            int ca; object category = _mapCategory.TryGetValue(m.CategoryCode, out ca) ? (object)ca : DBNull.Value;
            int b; object branch = _branchByOffice.TryGetValue(m.Office, out b) ? (object)b : DBNull.Value;

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                int detN = 0, detSkip = 0;
                try
                {
                    int code;
                    string ins = @"
                        INSERT INTO inqcs
                            (cors, inqref, inqdate, cscode, csbranch, cperson, executive, status, Category,
                             ordertype, sources, currency, exchangerate, ponumber, podate, shippingbranch,
                             inqlink, inqcode, terms, comcode, branchcode, remark, createdby, updatedby, createdon, updatedon)
                        OUTPUT INSERTED.code
                        VALUES (@cors, @inqref, @inqdate, @cscode, @csbranch, @cperson, @exec, @status, @cat,
                                @ordertype, @sources, @currency, @exrate, @pono, @podate, @shipbranch,
                                @inqlink, @inqcode, @terms, @com, @branch, @remark, @cb, @cb, @con, @uon)";
                    using (SqlCommand c = new SqlCommand(ins, tgt, tx))
                    {
                        c.Parameters.AddWithValue("@cors", Cors);
                        c.Parameters.AddWithValue("@inqref", PS(m.Inqref));
                        c.Parameters.AddWithValue("@inqdate", m.Inqdate);
                        c.Parameters.AddWithValue("@cscode", cscode);
                        c.Parameters.AddWithValue("@csbranch", csbranch);
                        c.Parameters.AddWithValue("@cperson", PS(cperson));
                        c.Parameters.AddWithValue("@exec", m.Executive);
                        c.Parameters.AddWithValue("@status", status);
                        c.Parameters.AddWithValue("@cat", category);
                        c.Parameters.AddWithValue("@ordertype", PS(m.OrderType));
                        c.Parameters.AddWithValue("@sources", PS(m.Sources));
                        c.Parameters.AddWithValue("@currency", m.Currency);
                        c.Parameters.AddWithValue("@exrate", m.ExchangeRate);
                        c.Parameters.AddWithValue("@pono", PS(m.Ponumber));
                        c.Parameters.AddWithValue("@podate", m.Podate);
                        c.Parameters.AddWithValue("@shipbranch", m.ShippingBranch);
                        c.Parameters.AddWithValue("@inqlink", m.InqLink);
                        c.Parameters.AddWithValue("@inqcode", m.InqCode);
                        c.Parameters.AddWithValue("@terms", DBNull.Value);
                        c.Parameters.AddWithValue("@com", CompanyCode);
                        c.Parameters.AddWithValue("@branch", branch);
                        c.Parameters.AddWithValue("@remark", PS(m.Remark));
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        c.Parameters.AddWithValue("@con", m.Createdon);
                        c.Parameters.AddWithValue("@uon", m.Updatedon);
                        code = Convert.ToInt32(c.ExecuteScalar());
                    }

                    string dkey = m.Office + "|" + m.SourceCode;
                    decimal itemsAll, lineTax, gstRate; List<object[]> chg;
                    _itemsAllByKey.TryGetValue(dkey, out itemsAll); _lineTaxByKey.TryGetValue(dkey, out lineTax); _chargesByKey.TryGetValue(dkey, out chg);
                    List<object[]> adj = ComputeCharges(itemsAll, chg, out gstRate);
                    decimal migratedValue = 0m; int discN = 0;

                    List<decimal[]> plist;
                    if (_hdrProductsByKey.TryGetValue(dkey, out plist))
                    {
                        foreach (decimal[] p in plist)
                        {
                            int pcode;
                            if (!_prodByItem.TryGetValue((int)p[0], out pcode)) { detSkip++; continue; }
                            int lc; object lineCur = _currencyMap.TryGetValue((int)p[4], out lc) ? (object)lc : DBNull.Value;
                            // eBizWiz nrate is already net of ndiscountperc -> price = master rate, discount = per-unit (master - nrate). - Aftab Alam
                            object price, disc = DBNull.Value, dpct = DBNull.Value;
                            if (p[3] != 0 && p[5] > p[2]) { price = p[5]; disc = p[5] - p[2]; dpct = p[3]; discN++; }
                            else price = p[2] == 0 ? (object)DBNull.Value : p[2];
                            decimal lr = 0m; string tsn;
                            if (gstRate > 0) lr = gstRate;
                            else if ((int)p[6] > 0 && p[7] != 0 && _taxsetName.TryGetValue((int)p[6], out tsn)) lr = PercentIn(tsn);
                            using (SqlCommand d = new SqlCommand(@"INSERT INTO inqcsdet
                                                       (inqcode, pcode, quantity, price, discount, discountpercent, prodtax, currency, createdby, updatedby, createdon, updatedon)
                                                   VALUES (@i, @p, @q, @pr, @disc, @dp, @tax, @cur, @cb, @cb, @con, @uon)", tgt, tx))
                            {
                                d.Parameters.AddWithValue("@i", code);
                                d.Parameters.AddWithValue("@p", pcode);
                                d.Parameters.AddWithValue("@q", p[1]);
                                d.Parameters.AddWithValue("@pr", price);
                                d.Parameters.AddWithValue("@disc", disc);
                                d.Parameters.AddWithValue("@dp", dpct);
                                d.Parameters.AddWithValue("@tax", lr > 0 ? (object)lr : DBNull.Value);
                                d.Parameters.AddWithValue("@cur", lineCur);
                                d.Parameters.AddWithValue("@cb", MigrationUser);
                                d.Parameters.AddWithValue("@con", m.Createdon);
                                d.Parameters.AddWithValue("@uon", m.Updatedon);
                                d.ExecuteNonQuery();
                            }
                            migratedValue += p[1] * p[2];
                            detN++;
                        }
                    }

                    // Totals = eBizWiz: blank-item/unmapped lines and line Tax Set amounts -> named adjustments.
                    decimal otherItems = Math.Round(itemsAll - migratedValue, 2);
                    if (otherItems != 0) adj.Insert(0, new object[] { "Other items (eBizWiz)", 0m, otherItems });
                    if (lineTax != 0) adj.Add(new object[] { "Tax on items (eBizWiz)", 0m, lineTax });
                    int adjN = InsertInqAdjustments(tgt, tx, code, adj, m.Createdon, m.Updatedon);
                    _cAdj += adjN; _lineDisc += discN; if (gstRate > 0) _docGst++;

                    int clN = 0;
                    List<object[]> clist;
                    if (_checklistsByKey.TryGetValue(m.Office + "|" + m.SourceCode, out clist))
                    {
                        int sort = 0;
                        foreach (object[] ck in clist)
                        {
                            int ncheck = (int)ck[1];
                            bool done = (bool)ck[3];
                            string nm2; string title = _checkName.TryGetValue(ncheck.ToString(), out nm2) && nm2.Length > 0 ? nm2 : ("Check " + ncheck);

                            // Due date: source expected date; if none, done items keep done/created date, open items get a future date (per senior).
                            object duedate;
                            if (ck[2] != DBNull.Value) duedate = ck[2];
                            else if (done) duedate = ck[4] != DBNull.Value ? ck[4] : (ck[8] != DBNull.Value ? ck[8] : (object)DateTime.Today);
                            else duedate = DateTime.Today.AddDays(7);

                            int au; object assignto = UserByCode.TryGetValue((string)ck[5], out au) ? (object)au : DBNull.Value;
                            // createdby = MigrationUser (Admin), same as every other migrated record. The checklist tab
                            // shows Edit/Delete only to the row's creator (usercode == createdby), so Admin owns migrated items.
                            object createdby = MigrationUser;
                            object createdon = ck[8] != DBNull.Value ? ck[8] : m.Createdon;
                            object updatedon = done ? (ck[4] != DBNull.Value ? ck[4] : (ck[9] != DBNull.Value ? ck[9] : createdon))
                                                    : (ck[9] != DBNull.Value ? ck[9] : createdon);

                            // Task checklist: module='SO', modulecode = Sales Order inqcs.code (no task record). Distinct tag
                            // so it never collides with the real Task module ('TSK') or Sales Invoice ('SAL'). - Aftab Alam
                            using (SqlCommand q = new SqlCommand(@"INSERT INTO taskchecklist
                                                       (title, status, module, modulecode, duedate, assignto, remark, sort, createdby, updatedby, createdon, updatedon)
                                                   VALUES (@t, @s, 'SO', @mc, @dd, @at, @rm, @sr, @cb, @cb, @con, @uon)", tgt, tx))
                            {
                                q.Parameters.AddWithValue("@t", PS(title));
                                q.Parameters.AddWithValue("@s", done ? 1 : 0);
                                q.Parameters.AddWithValue("@mc", code);
                                q.Parameters.AddWithValue("@dd", duedate);
                                q.Parameters.AddWithValue("@at", assignto);
                                q.Parameters.AddWithValue("@rm", PS((string)ck[6]));
                                q.Parameters.AddWithValue("@sr", sort++);
                                q.Parameters.AddWithValue("@cb", createdby);
                                q.Parameters.AddWithValue("@con", createdon);
                                q.Parameters.AddWithValue("@uon", updatedon);
                                q.ExecuteNonQuery();
                            }
                            clN++;
                        }
                    }

                    tx.Commit();
                    _cRows++; _cDet += detN; _cDetSkipped += detSkip; _cChecklist += clN;
                    if (_cRows % 1000 == 0) Console.WriteLine("  ... " + _cRows + " Sales Order migrated");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        static void LoadSourceMasters()
        {
            _fixed = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _inqCategory = SrcLookupInt("SELECT ncode, vname FROM mstinquirycategory WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
        }

        static void LoadChecklists()
        {
            foreach (DataRow dr in GetSrc("SELECT nofficeid, ncode, vname FROM mstchecks WITH (NOLOCK)").Tables[0].Rows)
                _checkName[Int(dr["ncode"]).ToString()] = Str(dr["vname"]);   // mstchecks.ncode is global/unique (NOT office-scoped)

            int rows = 0;
            foreach (DataRow dr in GetSrc("SELECT nofficeid, nordrc, ncheck, dexpdate, bdone, ddonedate, nallotedto, vremarks, addedby, addedon, editedon, ncode FROM trdordrc5checks WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nordrc, ncode").Tables[0].Rows)
            {
                string key = Int(dr["nofficeid"]) + "|" + Int(dr["nordrc"]);
                List<object[]> list;
                if (!_checklistsByKey.TryGetValue(key, out list)) { list = new List<object[]>(); _checklistsByKey[key] = list; }
                list.Add(new object[]
                {
                    Int(dr["nofficeid"]),   // 0 office
                    Int(dr["ncheck"]),      // 1 check master ncode
                    P(dr["dexpdate"]),      // 2 expected date
                    Bool(dr["bdone"]),      // 3 done
                    P(dr["ddonedate"]),     // 4 done date
                    NKey(dr["nallotedto"]), // 5 alloted-to source user NKey
                    Str(dr["vremarks"]),    // 6 remarks
                    NKey(dr["addedby"]),    // 7 added-by source user NKey
                    P(dr["addedon"]),       // 8 added on
                    P(dr["editedon"]),      // 9 edited on
                });
                rows++;
            }
            Console.WriteLine("  Check lists loaded: " + rows + " items across " + _checklistsByKey.Count + " orders (checks master " + _checkName.Count + ")");
        }

        static void EnsureStatuses(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // SO status dropdown reads status where module='Sales Order' (app/inquiry.asp) — match/seed under 'Sales Order', not 'Inquiry'. - Aftab Alam
            foreach (DataRow r in GetTgt("SELECT code, name FROM status WHERE module='Sales Order'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            // Seed the FULL fixedselection nordrecdstatus group so the SO form shows all client statuses (not only used). - Aftab Alam
            foreach (DataRow row in GetSrc("SELECT DISTINCT vdisplayvalue AS v FROM mstfixedselection WITH (NOLOCK) WHERE vfieldname='nordrecdstatus'").Tables[0].Rows)
            {
                string nm = Str(row["v"]).Trim();
                if (nm.Length == 0 || tmap.ContainsKey(nm)) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO status (name, module, sort, makedefault, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, 'Sales Order', 0, 0, @cb, @cb, GETDATE(), GETDATE())", tgt))
                { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); tmap[nm] = Convert.ToInt32(ins.ExecuteScalar()); }
                _newStatus++;
            }
            // Map the used source codes -> their status code (by name).
            foreach (DataRow row in GetSrc("SELECT DISTINCT nordrecdstatus FROM trhordrc WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND nordrecdstatus IS NOT NULL").Tables[0].Rows)
            {
                int scode = Int(row[0]);
                string nm = LookInt(_fixed, scode);
                if (nm.Length == 0) continue;
                int tc;
                if (tmap.TryGetValue(nm, out tc)) _mapStatus[scode] = tc;
            }
        }

        // Seed the full fixedselection nordrecdtype group into miscellaneous(Inquiry/Order Type) so the SO form's
        // Order Type dropdown (inqMiscNameSelect 'Order Type') shows all client options (SO ordertype stores the name). - Aftab Alam
        static void SeedOrderType(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM miscellaneous WHERE module='Inquiry' AND type='Order Type'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (DataRow row in GetSrc("SELECT DISTINCT vdisplayvalue AS v FROM mstfixedselection WITH (NOLOCK) WHERE vfieldname='nordrecdtype'").Tables[0].Rows)
            {
                string n = Str(row["v"]).Trim();
                if (n.Length == 0 || tmap.ContainsKey(n)) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Inquiry','Order Type', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                { ins.Parameters.AddWithValue("@n", n); ins.Parameters.AddWithValue("@cb", MigrationUser); tmap[n] = Convert.ToInt32(ins.ExecuteScalar()); }
            }
        }

        static void SeedCategories(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM miscellaneous WHERE module='Inquiry' AND type='Category'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (KeyValuePair<int, string> kv in _inqCategory)
            {
                string n = (kv.Value ?? "").Trim(); if (n.Length == 0) continue;
                int code;
                if (!tmap.TryGetValue(n, out code))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Inquiry','Category', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", n); ins.Parameters.AddWithValue("@cb", MigrationUser); code = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[n] = code; _newCategory++;
                }
                _mapCategory[kv.Key] = code;
            }
        }

        static void BuildCurrencyMap(SqlConnection tgt)
        {
            Dictionary<string, int> t = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name, sname FROM currency").Tables[0].Rows)
            {
                int code = Int(r["code"]); string nm = Str(r["name"]).Trim(), sn = Str(r["sname"]).Trim();
                if (sn.Length > 0 && !t.ContainsKey(sn)) t[sn] = code;
                if (nm.Length > 0 && !t.ContainsKey(nm)) t[nm] = code;
            }
            foreach (DataRow r in GetSrc("SELECT ncode, vmajordenomination, vmajorshortname FROM mstcurrency WITH (NOLOCK)").Tables[0].Rows)
            {
                int sc = Int(r["ncode"]); string nm = Str(r["vmajordenomination"]).Trim(), sn = Str(r["vmajorshortname"]).Trim();
                int a;
                if (sn.Length > 0 && t.TryGetValue(sn, out a)) _currencyMap[sc] = a;
                else if (nm.Length > 0 && t.TryGetValue(nm, out a)) _currencyMap[sc] = a;
            }
        }

        static void BuildCustomerMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM contact WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }

            foreach (DataRow r in GetSrc("SELECT ncode, ntitle, vname FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = NKey(r["ncode"]); if (key.Length == 0) continue;
                string nm = Cap(Join(LookInt(_fixed, Int(r["ntitle"])), Str(r["vname"])), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_custByParty.ContainsKey(key)) _custByParty[key] = code;
            }
            foreach (KeyValuePair<string, int> kv in PartyToContact) _custByParty[kv.Key] = kv.Value;
        }

        static void BuildBranchByContact(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, ccode, isnull(isdefault,0) as isdefault FROM mltaddress WITH (NOLOCK) ORDER BY ccode, isdefault DESC, code").Tables[0].Rows)
            {
                int ccode = Int(r["ccode"]); if (ccode <= 0) continue;
                if (!_branchByContact.ContainsKey(ccode)) _branchByContact[ccode] = Int(r["code"]);
            }
        }

        static void BuildProductMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM product WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }

            foreach (DataRow r in GetSrc("SELECT ncode, vname FROM mstitems WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                int ncode = Int(r["ncode"]);
                string nm = Cap(Str(r["vname"]), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_prodByItem.ContainsKey(ncode)) _prodByItem[ncode] = code;
            }
        }

        static void LoadPersons(SqlConnection tgt)
        {
            Dictionary<string, int> mltMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, ccode, name FROM mltcontact WITH (NOLOCK)").Tables[0].Rows)
            {
                int code = Int(r["code"]), ccode = Int(r["ccode"]); string name = Str(r["name"]);
                if (code > 0 && ccode > 0 && name.Length > 0) { string k = ccode + "|" + name; if (!mltMap.ContainsKey(k)) mltMap[k] = code; }
            }

            foreach (DataRow dr in GetSrc("SELECT nparty, ncode, ntitle, vcontactperson FROM msdparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND LTRIM(RTRIM(ISNULL(vcontactperson,''))) <> ''").Tables[0].Rows)
            {
                string partyKey = NKey(dr["nparty"]); if (partyKey.Length == 0) continue;
                int personNo = Int(dr["ncode"]); if (personNo <= 0) continue;
                string key = partyKey + "|" + personNo;
                if (_persons.ContainsKey(key)) continue;

                string rawName = Str(dr["vcontactperson"]);
                string formattedName = Cap(Join(LookInt(_fixed, Int(dr["ntitle"])), rawName), 150);
                string mltCodeStr = "";
                int ccode2, mcode, mcode2;
                if (_custByParty.TryGetValue(partyKey, out ccode2))
                {
                    if (mltMap.TryGetValue(ccode2 + "|" + formattedName, out mcode)) mltCodeStr = mcode.ToString();
                    else if (mltMap.TryGetValue(ccode2 + "|" + rawName, out mcode2)) mltCodeStr = mcode2.ToString();
                }
                _persons[key] = mltCodeStr;
            }
        }

        static void LoadHeaderProducts()
        {
            // ALL lines feed the item total + line tax (eBizWiz total); only item lines become inqcsdet.
            // decimal[] = { item, qty, nrate, ndiscountperc, currency, nmasterrate, ntaxset, ntaxamt }
            foreach (DataRow r in GetSrc("SELECT nofficeid, nordrc, nitem, nquantity, nrate, ndiscountperc, nitemcurrency, nmasterrate, ntaxset, ntaxamt FROM trdordrc1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, nordrc, ncode").Tables[0].Rows)
            {
                int office = Int(r[0]);
                int hdr = Int(r[1]); if (hdr <= 0) continue;
                string key = office + "|" + hdr;
                decimal cur;
                _itemsAllByKey.TryGetValue(key, out cur); _itemsAllByKey[key] = cur + Dec(r[3]) * Dec(r[4]);
                _lineTaxByKey.TryGetValue(key, out cur); _lineTaxByKey[key] = cur + Dec(r[9]);
                int item = Int(r[2]); if (item <= 0) continue;
                List<decimal[]> list;
                if (!_hdrProductsByKey.TryGetValue(key, out list)) { list = new List<decimal[]>(); _hdrProductsByKey[key] = list; }
                list.Add(new decimal[] { item, Dec(r[3]), Dec(r[4]), Dec(r[5]), Int(r[6]), Dec(r[7]), Int(r[8]), Dec(r[9]) });
            }
        }

        static void EnsureBranches(SqlConnection tgt)
        {
            foreach (int office in Offices)
            {
                string nm;
                string place = _officeName.TryGetValue(office, out nm) && nm.Length > 0 ? (nm.Length > 100 ? nm.Substring(0, 100) : nm) : "Office " + office;
                using (SqlCommand chk = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                {
                    chk.Parameters.AddWithValue("@cc", CompanyCode);
                    chk.Parameters.AddWithValue("@pl", place);
                    object o = chk.ExecuteScalar();
                    if (o != null && o != DBNull.Value) _branchByOffice[office] = Convert.ToInt32(o);
                }
            }
        }

        static void BuildInqLink(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, branchcode, inqref, cors FROM inqcs WITH (NOLOCK) WHERE cors IN ('CQ','CI') AND branchcode IS NOT NULL AND inqref IS NOT NULL").Tables[0].Rows)
            {
                string key = Int(r["branchcode"]) + "|" + Str(r["inqref"]).Trim();
                if (Str(r["cors"]) == "CQ") { if (!_cqByKey.ContainsKey(key)) _cqByKey[key] = Int(r["code"]); }
                else { if (!_ciByKey.ContainsKey(key)) _ciByKey[key] = Int(r["code"]); }
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhquote WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = Int(r["nofficeid"]) + "|" + Int(r["ncode"]);
                if (!_srcQuoteRef.ContainsKey(key)) _srcQuoteRef[key] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhinqry WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = Int(r["nofficeid"]) + "|" + Int(r["ncode"]);
                if (!_srcInqRef.ContainsKey(key)) _srcInqRef[key] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            }
        }

        static void BuildPartyAddresses()
        {
            foreach (DataRow r in GetSrc("SELECT ncode, vbilladdress, vinstaddress FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = NKey(r["ncode"]); if (key.Length == 0) continue;
                string bill = Str(r["vbilladdress"]);
                string inst = Str(r["vinstaddress"]);
                _partyBillAddr[key] = bill.Length > 0 ? bill : inst;
                _partyInstAddr[key] = inst.Length > 0 ? inst : bill;
            }
        }

        static string Join(string a, string b) { return string.Join(" ", new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(); }

        // Bill/Ship party code -> migrated contact -> its default mltaddress branch code (int). - Aftab Alam
        static object ResolveBranch(string partyKey)
        {
            int cid, br;
            if (partyKey.Length > 0 && _custByParty.TryGetValue(partyKey, out cid) && _branchByContact.TryGetValue(cid, out br))
                return br;
            return DBNull.Value;
        }

        class InqRow
        {
            public int SourceCode, Office, PersonNo, PersonNo2, StatusCode, CategoryCode;
            public string Inqref = "", PartyKey = "", OrderType = "", Sources = "", Ponumber = "", Remark = "";
            public object Executive = MigrationUser;
            public object Inqdate = DBNull.Value, Podate = DBNull.Value, Createdon = DBNull.Value, Updatedon = DBNull.Value,
                          Currency = DBNull.Value, ExchangeRate = DBNull.Value, InqLink = DBNull.Value, InqCode = DBNull.Value,
                          BuyerBranch = DBNull.Value, ShippingBranch = DBNull.Value;
        }
    }

    // ==================================================================
    //  PURCHASE ORDER (Order Placed) : trhordpl/trdordpl1items -> pur_order (type='PO')/pur_order_det.
    //  GST/purchase side (/edify/gst/purchase). Reuses existing pur_order columns and adds
    //  billto/shipto/inquirycategory/pendingreason/invoiceno/invoicedate/oano/oadate (see 00_ALTERS).
    //  Payment schedule + actual payments NOT migrated (separate Payment module). - Aftab Alam
    // ==================================================================
    static class PurchaseOrder
    {
        static Dictionary<int, string> _fixed, _inqCategory, _pendReason, _officeName;
        static readonly Dictionary<int, int> _mapStatus = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _mapCategory = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _mapPendReason = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _currencyMap = new Dictionary<int, int>();
        static readonly Dictionary<string, int> _supByParty = new Dictionary<string, int>();
        static readonly Dictionary<int, int> _branchByContact = new Dictionary<int, int>();   // contact -> default mltaddress code (for Bill/Ship branch)
        static readonly Dictionary<int, int> _prodByItem = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _branchByOffice = new Dictionary<int, int>();
        static readonly Dictionary<string, string> _partyBillAddr = new Dictionary<string, string>();
        static readonly Dictionary<string, string> _partyInstAddr = new Dictionary<string, string>();
        static readonly Dictionary<string, List<decimal[]>> _hdrProductsByKey = new Dictionary<string, List<decimal[]>>();
        static readonly Dictionary<string, List<int>> _soLinksByPO = new Dictionary<string, List<int>>();   // office|PO ncode -> list of nordrcditemdetailncode
        static readonly Dictionary<string, int> _soLineToHdr = new Dictionary<string, int>();               // office|SO line ncode -> SO header ncode
        static readonly Dictionary<string, string> _soRefByOfficeHdr = new Dictionary<string, string>();    // office|SO header ncode -> inqref
        static readonly Dictionary<string, int> _soCodeByKey = new Dictionary<string, int>();               // branch|inqref -> inqcs.code (cors='SO')
        static int _cRows, _cDet, _cDetSkipped, _cErrors, _supResolved, _supUnresolved, _newStatus, _newCategory, _newPend, _soLinked;
        static Dictionary<int, int> _batchByProd = new Dictionary<int, int>();   // product -> Default Batch - Aftab Alam

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: Purchase Order (pur_order type='PO')");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            LoadSourceMasters();

            using (SqlConnection tgt = OpenTgt())
            {
                EnsureStatuses(tgt);
                SeedCategories(tgt);
                SeedPendingReasons(tgt);
                BuildCurrencyMap(tgt);
                BuildSupplierMap(tgt);
                BuildBranchByContact(tgt);
                EnsureUserByCode();
                BuildProductMap(tgt);
                _batchByProd = DefaultBatchByProduct();
                EnsureBranches(tgt);
                BuildPartyAddresses();
                LoadHeaderProducts();
                BuildSoLinks(tgt);

                Console.WriteLine("Maps: status=" + _mapStatus.Count + "(+" + _newStatus + "), category=" + _mapCategory.Count + "(+" + _newCategory +
                                  "), pendreason=" + _mapPendReason.Count + "(+" + _newPend + "), currency=" + _currencyMap.Count +
                                  ", suppliers=" + _supByParty.Count + ", products=" + _prodByItem.Count + ", POwithLines=" + _hdrProductsByKey.Count +
                                  ", productsWithDefaultBatch=" + _batchByProd.Count);
                Console.WriteLine("-----------------------------------------------------");

                string headerSql = @"
                    SELECT ncode, vtrnprefix, ntrnno, dtrndate, ntoparty, ncurrency, nprindiscountperc, dprindispatchdate,
                           vtermsconditions, vremarks, vcomment, nordplcdstatus, ninquirycategory, npendingreason,
                           nbillto, nshipto, vinvno, dinvdate, vrefno, drefdate, nofficeid, addedon, editedon
                    FROM trhordpl WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY ncode";

                using (SqlConnection src = OpenSrc())
                using (SqlCommand rc = new SqlCommand(headerSql, src))
                {
                    rc.CommandTimeout = 0;
                    using (SqlDataReader dr = rc.ExecuteReader())
                        while (dr.Read())
                        {
                            try { InsertRow(tgt, dr); }
                            catch (Exception ex) { _cErrors++; if (_cErrors <= 20) Console.WriteLine("  [ERROR] PO " + NKey(dr["ncode"]) + ": " + ex.Message); }
                        }
                }

                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("  Purchase Orders migrated : " + _cRows);
                Console.WriteLine("  pur_order_det lines      : " + _cDet + "  (skipped " + _cDetSkipped + ")");
                Console.WriteLine("  Supplier scode           : " + _supResolved + " resolved, " + _supUnresolved + " unresolved");
                Console.WriteLine("  Sales Order linked       : " + _soLinked + " (salesordercode -> migrated SO inqcs.code)");
                Console.WriteLine("  Masters added            : status " + _newStatus + ", category " + _newCategory + ", pending reason " + _newPend);
                Console.WriteLine("  Errors                   : " + _cErrors);
                Console.WriteLine("-----------------------------------------------------");
            }
        }

        static void InsertRow(SqlConnection tgt, SqlDataReader dr)
        {
            int office = Int(dr["nofficeid"]);
            int srcCode = Int(dr["ncode"]);
            string purorderno = (Str(dr["vtrnprefix"]) + Str(dr["ntrnno"])).Trim();

            object scode = DBNull.Value;
            string supKey = NKey(dr["ntoparty"]); int sc;
            if (supKey.Length > 0 && _supByParty.TryGetValue(supKey, out sc)) { scode = sc; _supResolved++; }
            else if (supKey.Length > 0) _supUnresolved++;

            int cu; object currency = _currencyMap.TryGetValue(Int(dr["ncurrency"]), out cu) ? (object)cu : DBNull.Value;
            object discount = dr["nprindiscountperc"] != DBNull.Value ? dr["nprindiscountperc"] : DBNull.Value;
            object deliverydate = P(dr["dprindispatchdate"]);
            object paynotes = PS(Str(dr["vtermsconditions"]));
            object remarks = PS(BuildRemark(dr));

            int st; object status = _mapStatus.TryGetValue(Int(dr["nordplcdstatus"]), out st) ? (object)st : DBNull.Value;
            int ca; object category = _mapCategory.TryGetValue(Int(dr["ninquirycategory"]), out ca) ? (object)ca : DBNull.Value;
            int pr; object pendreason = _mapPendReason.TryGetValue(Int(dr["npendingreason"]), out pr) ? (object)pr : DBNull.Value;

            // Bill To / Ship To = branch reference (mltaddress code): party -> migrated contact -> its default branch. - Aftab Alam
            object buyerBranch = ResolveBranch(NKey(dr["nbillto"]));
            object shipBranch = ResolveBranch(NKey(dr["nshipto"]));

            object invoiceno = PS(Cap(Str(dr["vinvno"]), 100));
            object invoicedate = P(dr["dinvdate"]);
            object oano = PS(Cap(Str(dr["vrefno"]), 100));
            object oadate = P(dr["drefdate"]);

            int b; object branch = _branchByOffice.TryGetValue(office, out b) ? (object)b : DBNull.Value;
            object salesordercode = ResolveSalesOrderCodes(office, srcCode, branch);

            object createdon = P(dr["addedon"]);
            object updatedon = dr["editedon"] != DBNull.Value ? dr["editedon"] : P(dr["addedon"]);

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                int detN = 0, detSkip = 0;
                try
                {
                    int code;
                    string ins = @"
                        INSERT INTO pur_order
                            (purorderno, purorderdt, scode, currency, discount, deliverydate, pay_notes, remarks,
                             status, salesordercode, inquirycategory, pendingreason, cbranchcode, shippingbranch, invoiceno, invoicedate, oano, oadate,
                             branchcode, executive, comcode, [type], createdby, updatedby, createdon, updatedon)
                        OUTPUT INSERTED.code
                        VALUES (@pono, @podt, @scode, @cur, @disc, @deldt, @pay, @rem,
                                @status, @soc, @cat, @pend, @buyerbranch, @shipbranch, @invno, @invdt, @oano, @oadt,
                                @branch, @exec, @com, 'PO', @cb, @cb, @con, @uon)";
                    using (SqlCommand c = new SqlCommand(ins, tgt, tx))
                    {
                        c.Parameters.AddWithValue("@pono", PS(purorderno));
                        c.Parameters.AddWithValue("@podt", P(dr["dtrndate"]));
                        c.Parameters.AddWithValue("@scode", scode);
                        c.Parameters.AddWithValue("@cur", currency);
                        c.Parameters.AddWithValue("@disc", discount);
                        c.Parameters.AddWithValue("@deldt", deliverydate);
                        c.Parameters.AddWithValue("@pay", paynotes);
                        c.Parameters.AddWithValue("@rem", remarks);
                        c.Parameters.AddWithValue("@status", status);
                        c.Parameters.AddWithValue("@soc", salesordercode);
                        c.Parameters.AddWithValue("@cat", category);
                        c.Parameters.AddWithValue("@pend", pendreason);
                        c.Parameters.AddWithValue("@buyerbranch", buyerBranch);
                        c.Parameters.AddWithValue("@shipbranch", shipBranch);
                        c.Parameters.AddWithValue("@invno", invoiceno);
                        c.Parameters.AddWithValue("@invdt", invoicedate);
                        c.Parameters.AddWithValue("@oano", oano);
                        c.Parameters.AddWithValue("@oadt", oadate);
                        c.Parameters.AddWithValue("@branch", branch);
                        c.Parameters.AddWithValue("@exec", MigrationUser);
                        c.Parameters.AddWithValue("@com", CompanyCode);
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        c.Parameters.AddWithValue("@con", createdon);
                        c.Parameters.AddWithValue("@uon", updatedon);
                        code = Convert.ToInt32(c.ExecuteScalar());
                    }

                    List<decimal[]> plist;
                    if (_hdrProductsByKey.TryGetValue(office + "|" + srcCode, out plist))
                    {
                        foreach (decimal[] p in plist)
                        {
                            int pcode;
                            if (!_prodByItem.TryGetValue((int)p[0], out pcode)) { detSkip++; continue; }
                            int lc; object lineCur = _currencyMap.TryGetValue((int)p[4], out lc) ? (object)lc : DBNull.Value;
                            int bt; object lineBatch = _batchByProd.TryGetValue(pcode, out bt) ? (object)bt : DBNull.Value;   // product's Default Batch - Aftab Alam
                            using (SqlCommand d = new SqlCommand(@"INSERT INTO pur_order_det
                                                       (purcode, prodcode, prodbatchcode, qty, price, discount, currency, createdby, updatedby, createdon, updatedon)
                                                   VALUES (@pc, @prod, @batch, @q, @pr, @disc, @cur, @cb, @cb, @con, @uon)", tgt, tx))
                            {
                                d.Parameters.AddWithValue("@pc", code);
                                d.Parameters.AddWithValue("@prod", pcode);
                                d.Parameters.AddWithValue("@batch", lineBatch);
                                d.Parameters.AddWithValue("@q", p[1]);
                                d.Parameters.AddWithValue("@pr", p[2] == 0 ? (object)DBNull.Value : p[2]);
                                // eBizWiz nrate is already net of the line discount (1,178 of 1,179 discounted lines) -> no discount again. - Aftab Alam
                                d.Parameters.AddWithValue("@disc", DBNull.Value);
                                d.Parameters.AddWithValue("@cur", lineCur);
                                d.Parameters.AddWithValue("@cb", MigrationUser);
                                d.Parameters.AddWithValue("@con", createdon);
                                d.Parameters.AddWithValue("@uon", updatedon);
                                d.ExecuteNonQuery();
                            }
                            detN++;
                        }
                    }

                    tx.Commit();
                    _cRows++; _cDet += detN; _cDetSkipped += detSkip;
                    if (salesordercode != DBNull.Value) _soLinked++;
                    if (_cRows % 1000 == 0) Console.WriteLine("  ... " + _cRows + " Purchase Orders migrated");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // salesordercode = comma-separated migrated SO inqcs.code (cors='SO'), resolved via the PO line's
        // nordrcditemdetailncode -> SO line -> SO header -> inqref -> inqcs. The view STRING_SPLITs this on ','
        // and joins inqcs to show the SO number + customer.
        static object ResolveSalesOrderCodes(int office, int poNcode, object branch)
        {
            if (branch == DBNull.Value) return DBNull.Value;
            List<int> detncodes;
            if (!_soLinksByPO.TryGetValue(office + "|" + poNcode, out detncodes)) return DBNull.Value;
            List<string> codes = new List<string>();
            foreach (int detnc in detncodes)
            {
                int sohdr; if (!_soLineToHdr.TryGetValue(office + "|" + detnc, out sohdr)) continue;
                string soref; if (!_soRefByOfficeHdr.TryGetValue(office + "|" + sohdr, out soref) || soref.Length == 0) continue;
                int socode; if (!_soCodeByKey.TryGetValue(Convert.ToInt32(branch) + "|" + soref, out socode)) continue;
                string s = socode.ToString(CultureInfo.InvariantCulture);
                if (!codes.Contains(s)) codes.Add(s);
            }
            return codes.Count > 0 ? (object)string.Join(",", codes) : DBNull.Value;
        }

        static string BuildRemark(SqlDataReader dr)
        {
            List<string> parts = new List<string>();
            string rem = Str(dr["vremarks"]); if (!string.IsNullOrWhiteSpace(rem)) parts.Add(rem);
            string cm = Str(dr["vcomment"]); if (!string.IsNullOrWhiteSpace(cm)) parts.Add("Comment: " + cm);
            return string.Join(" | ", parts);
        }

        static void LoadSourceMasters()
        {
            _fixed = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _inqCategory = SrcLookupInt("SELECT ncode, vname FROM mstinquirycategory WITH (NOLOCK)");
            _pendReason = SrcLookupInt("SELECT ncode, vname FROM mstcallpendingreasons WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
        }

        // Order Status -> status master (module='Purchase Order'); seed the used source statuses, then map.
        static void EnsureStatuses(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM status WHERE module='Purchase Order'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (DataRow row in GetSrc("SELECT DISTINCT nordplcdstatus FROM trhordpl WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND nordplcdstatus IS NOT NULL").Tables[0].Rows)
            {
                int scode = Int(row[0]);
                string nm = LookInt(_fixed, scode);
                if (nm.Length == 0) continue;
                int tc;
                if (!tmap.TryGetValue(nm, out tc))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO status (name, module, sort, makedefault, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, 'Purchase Order', 0, 0, @cb, @cb, GETDATE(), GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); tc = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[nm] = tc; _newStatus++;
                }
                _mapStatus[scode] = tc;
            }
        }

        // Inquiry Category -> miscellaneous(Inquiry/Category) (same master the CI/SO migration seeds).
        static void SeedCategories(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM miscellaneous WHERE module='Inquiry' AND type='Category'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (KeyValuePair<int, string> kv in _inqCategory)
            {
                string n = (kv.Value ?? "").Trim(); if (n.Length == 0) continue;
                int code;
                if (!tmap.TryGetValue(n, out code))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Inquiry','Category', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", n); ins.Parameters.AddWithValue("@cb", MigrationUser); code = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[n] = code; _newCategory++;
                }
                _mapCategory[kv.Key] = code;
            }
        }

        // Pending Reason -> miscellaneous(Purchase Order/Pending Reason); source master mstcallpendingreasons.
        static void SeedPendingReasons(SqlConnection tgt)
        {
            Dictionary<string, int> tmap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM miscellaneous WHERE module='Purchase Order' AND type='Pending Reason'").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !tmap.ContainsKey(n)) tmap[n] = Int(r["code"]); }

            foreach (DataRow row in GetSrc("SELECT DISTINCT npendingreason FROM trhordpl WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND npendingreason IS NOT NULL AND npendingreason > 0").Tables[0].Rows)
            {
                int scode = Int(row[0]);
                string nm = LookInt(_pendReason, scode);
                if (nm.Length == 0) continue;
                nm = Cap(nm, 200);
                int tc;
                if (!tmap.TryGetValue(nm, out tc))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Purchase Order','Pending Reason', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); tc = Convert.ToInt32(ins.ExecuteScalar()); }
                    tmap[nm] = tc; _newPend++;
                }
                _mapPendReason[scode] = tc;
            }
        }

        static void BuildCurrencyMap(SqlConnection tgt)
        {
            Dictionary<string, int> t = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name, sname FROM currency").Tables[0].Rows)
            {
                int code = Int(r["code"]); string nm = Str(r["name"]).Trim(), sn = Str(r["sname"]).Trim();
                if (sn.Length > 0 && !t.ContainsKey(sn)) t[sn] = code;
                if (nm.Length > 0 && !t.ContainsKey(nm)) t[nm] = code;
            }
            foreach (DataRow r in GetSrc("SELECT ncode, vmajordenomination, vmajorshortname FROM mstcurrency WITH (NOLOCK)").Tables[0].Rows)
            {
                int sc = Int(r["ncode"]); string nm = Str(r["vmajordenomination"]).Trim(), sn = Str(r["vmajorshortname"]).Trim();
                int a;
                if (sn.Length > 0 && t.TryGetValue(sn, out a)) _currencyMap[sc] = a;
                else if (nm.Length > 0 && t.TryGetValue(nm, out a)) _currencyMap[sc] = a;
            }
        }

        static void BuildSupplierMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM contact WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }

            foreach (DataRow r in GetSrc("SELECT ncode, ntitle, vname FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = NKey(r["ncode"]); if (key.Length == 0) continue;
                string nm = Cap(Join(LookInt(_fixed, Int(r["ntitle"])), Str(r["vname"])), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_supByParty.ContainsKey(key)) _supByParty[key] = code;
            }
            foreach (KeyValuePair<string, int> kv in PartyToContact) _supByParty[kv.Key] = kv.Value;
        }

        static void BuildProductMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM product WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }

            foreach (DataRow r in GetSrc("SELECT ncode, vname FROM mstitems WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                int ncode = Int(r["ncode"]);
                string nm = Cap(Str(r["vname"]), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_prodByItem.ContainsKey(ncode)) _prodByItem[ncode] = code;
            }
        }

        static void EnsureBranches(SqlConnection tgt)
        {
            foreach (int office in Offices)
            {
                string nm;
                string place = _officeName.TryGetValue(office, out nm) && nm.Length > 0 ? (nm.Length > 100 ? nm.Substring(0, 100) : nm) : "Office " + office;
                using (SqlCommand chk = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                {
                    chk.Parameters.AddWithValue("@cc", CompanyCode);
                    chk.Parameters.AddWithValue("@pl", place);
                    object o = chk.ExecuteScalar();
                    if (o != null && o != DBNull.Value) _branchByOffice[office] = Convert.ToInt32(o);
                }
            }
        }

        static void BuildPartyAddresses()
        {
            foreach (DataRow r in GetSrc("SELECT ncode, vbilladdress, vinstaddress FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = NKey(r["ncode"]); if (key.Length == 0) continue;
                string bill = Str(r["vbilladdress"]);
                string inst = Str(r["vinstaddress"]);
                _partyBillAddr[key] = bill.Length > 0 ? bill : inst;
                _partyInstAddr[key] = inst.Length > 0 ? inst : bill;
            }
        }

        // contact -> its default mltaddress code (for Bill/Ship branch resolution). - Aftab Alam
        static void BuildBranchByContact(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, ccode, isnull(isdefault,0) as isdefault FROM mltaddress WITH (NOLOCK) ORDER BY ccode, isdefault DESC, code").Tables[0].Rows)
            {
                int ccode = Int(r["ccode"]); if (ccode <= 0) continue;
                if (!_branchByContact.ContainsKey(ccode)) _branchByContact[ccode] = Int(r["code"]);
            }
        }

        // Bill/Ship party code -> migrated contact (_supByParty) -> its default mltaddress branch code (int). - Aftab Alam
        static object ResolveBranch(string partyKey)
        {
            int cid, br;
            if (partyKey.Length > 0 && _supByParty.TryGetValue(partyKey, out cid) && _branchByContact.TryGetValue(cid, out br))
                return br;
            return DBNull.Value;
        }

        static void LoadHeaderProducts()
        {
            // All lines are read: the Sales Order link (nordrcditemdetailncode) is kept even on a blank-item line
            // (e.g. PO OP1606 links OR2128/OR2129 only via item-less lines); products are added only when nitem > 0. - Aftab Alam
            foreach (DataRow r in GetSrc("SELECT nofficeid, nordpl, nitem, nquantity, nrate, ndiscount, nitemcurrency, nordrcditemdetailncode FROM trdordpl1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, nordpl, ncode").Tables[0].Rows)
            {
                int office = Int(r[0]);
                int hdr = Int(r[1]); if (hdr <= 0) continue;
                int item = Int(r[2]);
                string key = office + "|" + hdr;

                if (item > 0)
                {
                    List<decimal[]> list;
                    if (!_hdrProductsByKey.TryGetValue(key, out list)) { list = new List<decimal[]>(); _hdrProductsByKey[key] = list; }
                    list.Add(new decimal[] { item, Dec(r[3]), Dec(r[4]), Dec(r[5]), Int(r[6]) });
                }

                int detnc = Int(r[7]);
                if (detnc > 0)
                {
                    List<int> links;
                    if (!_soLinksByPO.TryGetValue(key, out links)) { links = new List<int>(); _soLinksByPO[key] = links; }
                    if (!links.Contains(detnc)) links.Add(detnc);
                }
            }
        }

        // Build the maps to resolve PO line -> migrated Sales Order inqcs.code.
        static void BuildSoLinks(SqlConnection tgt)
        {
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, nordrc FROM trdordrc1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["ncode"]);
                if (!_soLineToHdr.ContainsKey(k)) _soLineToHdr[k] = Int(r["nordrc"]);
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhordrc WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["ncode"]);
                if (!_soRefByOfficeHdr.ContainsKey(k)) _soRefByOfficeHdr[k] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            }
            foreach (DataRow r in GetTgt("SELECT code, branchcode, inqref FROM inqcs WITH (NOLOCK) WHERE cors='SO' AND branchcode IS NOT NULL AND inqref IS NOT NULL").Tables[0].Rows)
            {
                string k = Int(r["branchcode"]) + "|" + Str(r["inqref"]).Trim();
                if (!_soCodeByKey.ContainsKey(k)) _soCodeByKey[k] = Int(r["code"]);
            }
        }

        static string Join(string a, string b) { return string.Join(" ", new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(); }
    }

    // ==================================================================
    //  MODULE 7 : SALES INVOICE  (eBizWiz Warranty/Non-Warranty Sales -> EdifyBiz sal_order type='SO')
    //  Source: trhsales (header), trdsales1items (lines), trdsales6checks (check list), trdsales4posttaxchgs (freight).
    //  TAX is NOT reconstructed: source keeps no per-line GST (only baked into ntotalamount). We migrate the real
    //  line items (taxable value) and NOTE the original grand total + freight + amount received in remarks. No fake
    //  GST/sal_tax (this is a live GST system). Payment = separate Payment module. - Aftab Alam
    // ==================================================================
    static class SalesInvoice
    {
        static Dictionary<int, string> _fixed, _officeName;
        static readonly Dictionary<string, int> _custByParty = new Dictionary<string, int>();
        static readonly Dictionary<int, int> _branchByContact = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _prodByItem = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _branchByOffice = new Dictionary<int, int>();
        static Dictionary<string, string> _persons;
        static readonly Dictionary<string, List<decimal[]>> _hdrProductsByKey = new Dictionary<string, List<decimal[]>>();
        static readonly Dictionary<string, decimal> _itemsAllByKey = new Dictionary<string, decimal>();    // office|sale -> sum(qty*rate) of ALL source lines (client item total)
        static readonly Dictionary<string, decimal> _lineTaxByKey = new Dictionary<string, decimal>();     // office|sale -> sum(line ntaxamt)
        static readonly Dictionary<string, List<object[]>> _chargesByKey = new Dictionary<string, List<object[]>>(); // office|sale -> { label, mode, pct, amount, itemonly } in entry order
        static readonly Dictionary<int, object[]> _custGstByBranch = new Dictionary<int, object[]>();      // mltaddress.code -> { stategstcode, state.code }
        static readonly Dictionary<int, string> _prodType = new Dictionary<int, string>();
        static Dictionary<int, string> _taxsetName;
        static int _taxC, _taxS, _taxI;
        static int _cTaxRows, _cAdj, _invTaxed, _invGstDiff, _billFromParty, _invLineDisc;
        static int _labelWarranty, _labelNonWarranty, _lblWarranty, _lblNonWarranty;

        // Sales label master (label.category = 'SAL', the list the sales Labels button reads): reuse by name, create if missing.
        static void EnsureWarrantyLabels(SqlConnection tgt)
        {
            _labelWarranty = SalesLabel(tgt, "Warranty Sales", "#29BF94");
            _labelNonWarranty = SalesLabel(tgt, "Non Warranty Sales", "#FBC617");
            Console.WriteLine("  label(SAL): Warranty Sales=" + _labelWarranty + ", Non Warranty Sales=" + _labelNonWarranty);
        }

        static int SalesLabel(SqlConnection tgt, string name, string color)
        {
            using (SqlCommand q = new SqlCommand("SELECT TOP 1 code FROM label WHERE category='SAL' AND name=@n ORDER BY code", tgt))
            {
                q.Parameters.AddWithValue("@n", name);
                object o = q.ExecuteScalar();
                if (o != null && o != DBNull.Value) return Convert.ToInt32(o);
            }
            using (SqlCommand ins = new SqlCommand("INSERT INTO label (name, category, color, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES (@n, 'SAL', @c, @cb, GETDATE(), @cb, GETDATE())", tgt))
            {
                ins.Parameters.AddWithValue("@n", name); ins.Parameters.AddWithValue("@c", color); ins.Parameters.AddWithValue("@cb", MigrationUser);
                return Convert.ToInt32(ins.ExecuteScalar());
            }
        }
        static readonly Dictionary<string, object[]> _serialByLine = new Dictionary<string, object[]>();   // office|line ncode -> { serialsCsv, warrantymonths, installdate, location }
        static readonly Dictionary<string, string> _checkName = new Dictionary<string, string>();
        static readonly Dictionary<string, List<object[]>> _checklistsByKey = new Dictionary<string, List<object[]>>();
        static readonly Dictionary<string, string> _soRefByOfficeHdr = new Dictionary<string, string>();  // office|trhordrc ncode -> inqref
        static readonly Dictionary<string, string> _cqRefByOfficeHdr = new Dictionary<string, string>();  // office|trhquote ncode -> inqref
        static readonly Dictionary<string, int> _soCodeByKey = new Dictionary<string, int>();              // branch|inqref -> inqcs.code (cors='SO')
        static readonly Dictionary<string, int> _cqCodeByKey = new Dictionary<string, int>();              // branch|inqref -> inqcs.code (cors='CQ')
        static int _inrCode;
        static int _cRows, _cDet, _cDetSkipped, _cErrors, _custResolved, _custUnresolved, _cChecklist, _linked;
        static Dictionary<int, int> _batchByProd = new Dictionary<int, int>();   // product -> Default Batch (Batch No is required on invoice lines) - Aftab Alam

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: Sales Invoice (sal_order type='SO')");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            LoadSourceMasters();

            using (SqlConnection tgt = OpenTgt())
            {
                SeedSalesTypes(tgt);
                EnsureWarrantyLabels(tgt);
                BuildCurrencyMap(tgt);
                BuildCustomerMap(tgt);
                BuildBranchByContact(tgt);
                EnsureUserByCode();
                BuildProductMap(tgt);
                _batchByProd = DefaultBatchByProduct();
                EnsureBranches(tgt);
                LoadPersons(tgt);
                LoadHeaderProducts();
                LoadSerialWarranty();
                LoadCharges();
                LoadChecklists();
                BuildInqcssoLinks(tgt);
                EnsureGstSetup(tgt);
                LoadTaxMaps(tgt);

                Console.WriteLine("Maps: INR=" + _inrCode + ", customers=" + _custByParty.Count + ", products=" + _prodByItem.Count +
                                  ", persons=" + _persons.Count + ", salesWithLines=" + _hdrProductsByKey.Count + ", SO=" + _soCodeByKey.Count + ", CQ=" + _cqCodeByKey.Count +
                                  ", productsWithDefaultBatch=" + _batchByProd.Count);
                Console.WriteLine("-----------------------------------------------------");

                string headerSql = @"
                    SELECT ncode, vtrnprefix, ntrnno, dtrndate, nparty, npartycontact, nsalesman, nsalestype,
                           nbillto, nshipto, vpono, dpodate, ndispatchmode, vdespatchthru, vdespdocno, ddespdocdate,
                           nquote, nordrc, vrefno, drefdate, vremarks, vcomment, nitemtotal, ntotalamount, namountrecd, bwarranty,
                           nofficeid, addedon, editedon
                    FROM trhsales WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY ncode";

                using (SqlConnection src = OpenSrc())
                using (SqlCommand rc = new SqlCommand(headerSql, src))
                {
                    rc.CommandTimeout = 0;
                    using (SqlDataReader dr = rc.ExecuteReader())
                        while (dr.Read())
                        {
                            try { InsertRow(tgt, dr); }
                            catch (Exception ex) { _cErrors++; if (_cErrors <= 20) Console.WriteLine("  [ERROR] Sale " + NKey(dr["ncode"]) + ": " + ex.Message); }
                        }
                }

                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("  Sales Invoices migrated : " + _cRows);
                Console.WriteLine("  sal_order_det lines     : " + _cDet + "  (skipped " + _cDetSkipped + ")");
                Console.WriteLine("  Customer ccode          : " + _custResolved + " resolved, " + _custUnresolved + " unresolved");
                Console.WriteLine("  inqcsso linked          : " + _linked + " (to migrated SO/CQ)");
                Console.WriteLine("  Check list items        : " + _cChecklist);
                Console.WriteLine("  Bill To from party      : " + _billFromParty + " (source Bill To blank)");
                Console.WriteLine("  Labels                  : Warranty Sales " + _lblWarranty + ", Non Warranty Sales " + _lblNonWarranty);
                Console.WriteLine("  Tax: invoices taxed     : " + _invTaxed + ", sal_tax rows " + _cTaxRows + ", adjustments " + _cAdj + ", with GST difference line " + _invGstDiff + ", pre-GST discount on lines " + _invLineDisc);
                Console.WriteLine("  Errors                  : " + _cErrors);
                Console.WriteLine("-----------------------------------------------------");
            }
        }

        static void InsertRow(SqlConnection tgt, SqlDataReader dr)
        {
            int office = Int(dr["nofficeid"]);
            int srcCode = Int(dr["ncode"]);
            string salorderno = (Str(dr["vtrnprefix"]) + Str(dr["ntrnno"])).Trim();

            object ccode = DBNull.Value;
            string partyKey = NKey(dr["nparty"]); int cc;
            if (partyKey.Length > 0 && _custByParty.TryGetValue(partyKey, out cc)) { ccode = cc; _custResolved++; }
            else if (partyKey.Length > 0) _custUnresolved++;

            object contactperson = DBNull.Value;
            int personNo = Int(dr["npartycontact"]);
            if (personNo > 0 && partyKey.Length > 0)
            {
                string mlt; if (_persons.TryGetValue(partyKey + "|" + personNo, out mlt) && mlt.Length > 0) contactperson = Convert.ToInt32(mlt);
            }

            int ex; object executive = UserByCode.TryGetValue(NKey(dr["nsalesman"]), out ex) ? (object)ex : MigrationUser;

            object billBranch = ResolveBranch(NKey(dr["nbillto"]));
            // Blank Bill To in eBizWiz = the party itself -> party's branch (needed for the customer GST state). - Aftab Alam
            if (billBranch == DBNull.Value && NKey(dr["nbillto"]).Length == 0) { billBranch = ResolveBranch(partyKey); if (billBranch != DBNull.Value) _billFromParty++; }
            object shipBranch = ResolveBranch(NKey(dr["nshipto"]));

            object ordertype = PS(Cap(LookInt(_fixed, Int(dr["nsalestype"])), 50));
            object dispatchmode = PS(LookInt(_fixed, Int(dr["ndispatchmode"])));
            object ponumber = PS(Str(dr["vpono"]));
            object purorderdt = P(dr["dpodate"]);
            object couriername = PS(Str(dr["vdespatchthru"]));
            object courierno = PS(Str(dr["vdespdocno"]));
            object dispatchdate = P(dr["ddespdocdate"]);

            int b; object branch = _branchByOffice.TryGetValue(office, out b) ? (object)b : DBNull.Value;
            object inqcsso = ResolveInqcsso(office, branch, Int(dr["nordrc"]), Int(dr["nquote"]));

            object currency = _inrCode > 0 ? (object)_inrCode : DBNull.Value;
            object remarks = PS(BuildRemark(dr, office, srcCode));

            object createdon = P(dr["addedon"]);
            object updatedon = dr["editedon"] != DBNull.Value ? dr["editedon"] : P(dr["addedon"]);

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                int detN = 0, detSkip = 0, clN = 0;
                List<object[]> dets = new List<object[]>();                              // { sal_order_det.code, product, line value as the view computes it }
                Dictionary<int, decimal> prodLineRate = new Dictionary<int, decimal>();  // product -> line Tax Set IGST rate
                try
                {
                    int code;
                    string ins = @"
                        INSERT INTO sal_order
                            (salorderno, salorderdt, ccode, contactperson, executive, ordertype, inqcsso,
                             ponumber, purorderdt, dispatchmode, couriername, courierno, dispatchdate,
                             cbranchcode, shippingbranch, currency, remarks, branchcode, comcode, [type],
                             createdby, updatedby, createdon, updatedon)
                        OUTPUT INSERTED.code
                        VALUES (@no, @dt, @cc, @cp, @exec, @ot, @link,
                                @pono, @podt, @dm, @cn, @cno, @ddt,
                                @bill, @ship, @cur, @rem, @branch, @com, 'SO',
                                @cb, @cb, @con, @uon)";
                    using (SqlCommand c = new SqlCommand(ins, tgt, tx))
                    {
                        c.Parameters.AddWithValue("@no", PS(salorderno));
                        c.Parameters.AddWithValue("@dt", P(dr["dtrndate"]));
                        c.Parameters.AddWithValue("@cc", ccode);
                        c.Parameters.AddWithValue("@cp", contactperson);
                        c.Parameters.AddWithValue("@exec", executive);
                        c.Parameters.AddWithValue("@ot", ordertype);
                        c.Parameters.AddWithValue("@link", inqcsso);
                        c.Parameters.AddWithValue("@pono", ponumber);
                        c.Parameters.AddWithValue("@podt", purorderdt);
                        c.Parameters.AddWithValue("@dm", dispatchmode);
                        c.Parameters.AddWithValue("@cn", couriername);
                        c.Parameters.AddWithValue("@cno", courierno);
                        c.Parameters.AddWithValue("@ddt", dispatchdate);
                        c.Parameters.AddWithValue("@bill", billBranch);
                        c.Parameters.AddWithValue("@ship", shipBranch);
                        c.Parameters.AddWithValue("@cur", currency);
                        c.Parameters.AddWithValue("@rem", remarks);
                        c.Parameters.AddWithValue("@branch", branch);
                        c.Parameters.AddWithValue("@com", CompanyCode);
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        c.Parameters.AddWithValue("@con", createdon);
                        c.Parameters.AddWithValue("@uon", updatedon);
                        code = Convert.ToInt32(c.ExecuteScalar());
                    }

                    List<decimal[]> plist;
                    if (_hdrProductsByKey.TryGetValue(office + "|" + srcCode, out plist))
                    {
                        foreach (decimal[] p in plist)
                        {
                            int pcode;
                            if (!_prodByItem.TryGetValue((int)p[0], out pcode)) { detSkip++; continue; }
                            // Serial / warranty for this line (by source line ncode p[4]). warrantymonths is NOT NULL -> default 0. - Aftab Alam
                            object[] sw; object wmonths = 0, instdt = DBNull.Value, loc = DBNull.Value, instrem = DBNull.Value;
                            if (_serialByLine.TryGetValue(office + "|" + (int)p[4], out sw))
                            {
                                if (sw[1] != DBNull.Value) wmonths = sw[1];
                                instdt = sw[2]; loc = sw[3];
                                if (((string)sw[0]).Length > 0) instrem = PS(Cap("Serial No: " + (string)sw[0], 1000));   // col widened to nvarchar(1000) in 00_ALTERS
                            }
                            int bt; object lineBatch = _batchByProd.TryGetValue(pcode, out bt) ? (object)bt : DBNull.Value;   // product's Default Batch - Aftab Alam
                            using (SqlCommand d = new SqlCommand(@"INSERT INTO sal_order_det
                                                       (salcode, prodcode, prodbatchcode, qty, price, discount, currency, warrantymonths, installationdt, location, installedremarks, createdby, updatedby, createdon, updatedon)
                                                   OUTPUT INSERTED.code
                                                   VALUES (@sc, @prod, @batch, @q, @pr, @disc, @cur, @wm, @idt, @loc, @irem, @cb, @cb, @con, @uon)", tgt, tx))
                            {
                                d.Parameters.AddWithValue("@sc", code);
                                d.Parameters.AddWithValue("@prod", pcode);
                                d.Parameters.AddWithValue("@batch", lineBatch);
                                d.Parameters.AddWithValue("@q", p[1]);
                                d.Parameters.AddWithValue("@pr", p[2] == 0 ? (object)DBNull.Value : p[2]);
                                // eBizWiz nrate is already the net rate (item total = qty x nrate; ndiscount is never deducted) -> no discount. - Aftab Alam
                                d.Parameters.AddWithValue("@disc", DBNull.Value);
                                d.Parameters.AddWithValue("@cur", currency);
                                d.Parameters.AddWithValue("@wm", wmonths);
                                d.Parameters.AddWithValue("@idt", instdt);
                                d.Parameters.AddWithValue("@loc", loc);
                                d.Parameters.AddWithValue("@irem", instrem);
                                d.Parameters.AddWithValue("@cb", MigrationUser);
                                d.Parameters.AddWithValue("@con", createdon);
                                d.Parameters.AddWithValue("@uon", updatedon);
                                int detCode = Convert.ToInt32(d.ExecuteScalar());
                                // view line value: type 's' shows qty 1 (sales.js), products qty x price
                                string pt; decimal val = (_prodType.TryGetValue(pcode, out pt) && pt == "s") ? p[2] : p[1] * p[2];
                                dets.Add(new object[] { detCode, pcode, val });
                            }
                            detN++;
                            string tsn; decimal lr;
                            if ((int)p[5] > 0 && p[6] != 0 && _taxsetName.TryGetValue((int)p[5], out tsn) && (lr = GstRateIn(tsn)) > 0 && !prodLineRate.ContainsKey(pcode)) prodLineRate[pcode] = lr;
                        }
                    }

                    int[] taxStat = ApplyTax(tgt, tx, code, office + "|" + srcCode, billBranch, dets, prodLineRate, createdon, updatedon);

                    // eBizWiz menu the bill came from: bwarranty 1 = Warranty Sales, 0 = Non Warranty Sales (Spare Part Sale Entry)
                    // -> standard sales label (label.category 'SAL', labelrelation.module = sal_order.code, as action=label saves). - Aftab Alam
                    bool warranty = Bool(dr["bwarranty"]);
                    using (SqlCommand lr = new SqlCommand("INSERT INTO labelrelation (label, module, createdon, createdby) VALUES (@l, @m, @con, @cb)", tgt, tx))
                    {
                        lr.Parameters.AddWithValue("@l", warranty ? _labelWarranty : _labelNonWarranty);
                        lr.Parameters.AddWithValue("@m", code);
                        lr.Parameters.AddWithValue("@con", createdon);
                        lr.Parameters.AddWithValue("@cb", MigrationUser);
                        lr.ExecuteNonQuery();
                    }

                    List<object[]> clist;
                    if (_checklistsByKey.TryGetValue(office + "|" + srcCode, out clist))
                    {
                        int sort = 0;
                        foreach (object[] ck in clist)
                        {
                            int ncheck = (int)ck[0];
                            bool done = (bool)ck[2];
                            string nm2; string title = _checkName.TryGetValue(ncheck.ToString(), out nm2) && nm2.Length > 0 ? nm2 : ("Check " + ncheck);
                            object duedate;
                            if (ck[1] != DBNull.Value) duedate = ck[1];
                            else if (done) duedate = ck[3] != DBNull.Value ? ck[3] : (ck[6] != DBNull.Value ? ck[6] : (object)DateTime.Today);
                            else duedate = DateTime.Today.AddDays(7);
                            int au; object assignto = UserByCode.TryGetValue((string)ck[4], out au) ? (object)au : DBNull.Value;
                            object clCreated = ck[6] != DBNull.Value ? ck[6] : createdon;
                            object clUpdated = done ? (ck[3] != DBNull.Value ? ck[3] : (ck[7] != DBNull.Value ? ck[7] : clCreated))
                                                    : (ck[7] != DBNull.Value ? ck[7] : clCreated);
                            using (SqlCommand q = new SqlCommand(@"INSERT INTO taskchecklist
                                                       (title, status, module, modulecode, duedate, assignto, remark, sort, createdby, updatedby, createdon, updatedon)
                                                   VALUES (@t, @s, 'SAL', @mc, @dd, @at, @rm, @sr, @cb, @cb, @con, @uon)", tgt, tx))
                            {
                                q.Parameters.AddWithValue("@t", PS(title));
                                q.Parameters.AddWithValue("@s", done ? 1 : 0);
                                q.Parameters.AddWithValue("@mc", code);
                                q.Parameters.AddWithValue("@dd", duedate);
                                q.Parameters.AddWithValue("@at", assignto);
                                q.Parameters.AddWithValue("@rm", PS((string)ck[5]));
                                q.Parameters.AddWithValue("@sr", sort++);
                                q.Parameters.AddWithValue("@cb", MigrationUser);
                                q.Parameters.AddWithValue("@con", clCreated);
                                q.Parameters.AddWithValue("@uon", clUpdated);
                                q.ExecuteNonQuery();
                            }
                            clN++;
                        }
                    }

                    tx.Commit();
                    _cRows++; _cDet += detN; _cDetSkipped += detSkip; _cChecklist += clN;
                    _cTaxRows += taxStat[0]; _cAdj += taxStat[1]; if (taxStat[0] > 0) _invTaxed++; if (taxStat[2] > 0) _invGstDiff++;
                    if (warranty) _lblWarranty++; else _lblNonWarranty++;
                    if (inqcsso != DBNull.Value) _linked++;
                    if (_cRows % 1000 == 0) Console.WriteLine("  ... " + _cRows + " Sales Invoices migrated");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // Bill/Ship = branch (cbranchcode/shippingbranch). Tax + charges are real rows now (sal_tax / sal_adjust);
        // remarks keep only the eBizWiz invoice total + amount received for reference. - Aftab Alam
        static string BuildRemark(SqlDataReader dr, int office, int srcCode)
        {
            List<string> parts = new List<string>();
            string rem = Str(dr["vremarks"]); if (!string.IsNullOrWhiteSpace(rem)) parts.Add(rem);
            string cm = Str(dr["vcomment"]); if (!string.IsNullOrWhiteSpace(cm)) parts.Add("Comment: " + cm);
            string refno = Str(dr["vrefno"]); if (!string.IsNullOrWhiteSpace(refno)) parts.Add("Ref No: " + refno);
            if (dr["drefdate"] != DBNull.Value) parts.Add("Ref Date: " + Convert.ToDateTime(dr["drefdate"]).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            decimal total = Dec(dr["ntotalamount"]);
            decimal recd = Dec(dr["namountrecd"]);
            if (total != 0) parts.Add("eBizWiz Invoice Total: " + total.ToString("0.##", CultureInfo.InvariantCulture));
            if (recd != 0) parts.Add("Amount Received: " + recd.ToString("0.##", CultureInfo.InvariantCulture));
            return string.Join(" | ", parts);
        }

        static object ResolveInqcsso(int office, object branch, int nordrc, int nquote)
        {
            if (branch == DBNull.Value) return DBNull.Value;
            int br = Convert.ToInt32(branch);
            if (nordrc > 0)
            {
                string soref; int socode;
                if (_soRefByOfficeHdr.TryGetValue(office + "|" + nordrc, out soref) && soref.Length > 0
                    && _soCodeByKey.TryGetValue(br + "|" + soref, out socode)) return socode;
            }
            if (nquote > 0)
            {
                string cqref; int cqcode;
                if (_cqRefByOfficeHdr.TryGetValue(office + "|" + nquote, out cqref) && cqref.Length > 0
                    && _cqCodeByKey.TryGetValue(br + "|" + cqref, out cqcode)) return cqcode;
            }
            return DBNull.Value;
        }

        static object ResolveBranch(string partyKey)
        {
            int cid, br;
            if (partyKey.Length > 0 && _custByParty.TryGetValue(partyKey, out cid) && _branchByContact.TryGetValue(cid, out br))
                return br;
            return DBNull.Value;
        }

        static void LoadSourceMasters()
        {
            _fixed = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
        }

        // Sales Type dropdown master: seed the FULL fixedselection nsalestype group into miscellaneous(Sales/Sales Type)
        // (form reads it by name; sal_order.ordertype stores the name). Same "seed full, not hardcoded" rule as SO Order Type. - Aftab Alam
        static void SeedSalesTypes(SqlConnection tgt)
        {
            HashSet<string> have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT name FROM miscellaneous WHERE module='Sales' AND type='Sales Type'").Tables[0].Rows) have.Add(Str(r["name"]).Trim());
            int added = 0;
            foreach (DataRow r in GetSrc("SELECT DISTINCT vdisplayvalue AS v FROM mstfixedselection WITH (NOLOCK) WHERE vfieldname='nsalestype'").Tables[0].Rows)
            {
                string nm = Cap(Str(r["v"]).Trim(), 50);
                if (nm.Length == 0 || have.Contains(nm)) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) VALUES ('Sales','Sales Type', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); ins.ExecuteNonQuery(); }
                have.Add(nm); added++;
            }
            Console.WriteLine("  miscellaneous(Sales/Sales Type) seeded: " + added + " (full master)");
        }

        static void BuildCurrencyMap(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, name, sname FROM currency").Tables[0].Rows)
            {
                string nm = Str(r["name"]).Trim(), sn = Str(r["sname"]).Trim();
                if (_inrCode == 0 && (sn.Equals("INR", StringComparison.OrdinalIgnoreCase) || nm.IndexOf("Indian Rupee", StringComparison.OrdinalIgnoreCase) >= 0))
                    _inrCode = Int(r["code"]);
            }
        }

        static void BuildCustomerMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM contact WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }
            foreach (DataRow r in GetSrc("SELECT ncode, ntitle, vname FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string key = NKey(r["ncode"]); if (key.Length == 0) continue;
                string nm = Cap(Join(LookInt(_fixed, Int(r["ntitle"])), Str(r["vname"])), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_custByParty.ContainsKey(key)) _custByParty[key] = code;
            }
            foreach (KeyValuePair<string, int> kv in PartyToContact) _custByParty[kv.Key] = kv.Value;
        }

        static void BuildBranchByContact(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, ccode, isnull(isdefault,0) as isdefault FROM mltaddress WITH (NOLOCK) ORDER BY ccode, isdefault DESC, code").Tables[0].Rows)
            {
                int ccode = Int(r["ccode"]); if (ccode <= 0) continue;
                if (!_branchByContact.ContainsKey(ccode)) _branchByContact[ccode] = Int(r["code"]);
            }
        }

        static void BuildProductMap(SqlConnection tgt)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM product WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !byName.ContainsKey(n)) byName[n] = Int(r["code"]); }
            foreach (DataRow r in GetSrc("SELECT ncode, vname FROM mstitems WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                int ncode = Int(r["ncode"]);
                string nm = Cap(Str(r["vname"]), 150);
                int code;
                if (nm.Length > 0 && byName.TryGetValue(nm, out code) && !_prodByItem.ContainsKey(ncode)) _prodByItem[ncode] = code;
            }
        }

        static void EnsureBranches(SqlConnection tgt)
        {
            foreach (int office in Offices)
            {
                string nm;
                string place = _officeName.TryGetValue(office, out nm) && nm.Length > 0 ? (nm.Length > 100 ? nm.Substring(0, 100) : nm) : "Office " + office;
                using (SqlCommand chk = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                {
                    chk.Parameters.AddWithValue("@cc", CompanyCode);
                    chk.Parameters.AddWithValue("@pl", place);
                    object o = chk.ExecuteScalar();
                    if (o != null && o != DBNull.Value) _branchByOffice[office] = Convert.ToInt32(o);
                }
            }
        }

        static void LoadPersons(SqlConnection tgt)
        {
            _persons = new Dictionary<string, string>();
            Dictionary<string, int> mltMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, ccode, name FROM mltcontact WITH (NOLOCK)").Tables[0].Rows)
            {
                int code = Int(r["code"]), ccode = Int(r["ccode"]); string name = Str(r["name"]);
                if (code > 0 && ccode > 0 && name.Length > 0) { string key = ccode + "|" + name; if (!mltMap.ContainsKey(key)) mltMap[key] = code; }
            }
            foreach (DataRow dr in GetSrc("SELECT nparty, ncode, ntitle, vcontactperson FROM msdparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND LTRIM(RTRIM(ISNULL(vcontactperson,''))) <> ''").Tables[0].Rows)
            {
                string partyKey = NKey(dr["nparty"]); if (partyKey.Length == 0) continue;
                int personNo = Int(dr["ncode"]); if (personNo <= 0) continue;
                string key = partyKey + "|" + personNo;
                if (_persons.ContainsKey(key)) continue;
                string rawName = Str(dr["vcontactperson"]);
                string title = LookInt(_fixed, Int(dr["ntitle"]));
                string formattedName = Cap(Join(title, rawName), 100);
                string mltCodeStr = "";
                int ccode2, mcode, mcode2;
                if (_custByParty.TryGetValue(partyKey, out ccode2))
                {
                    if (mltMap.TryGetValue(ccode2 + "|" + formattedName, out mcode)) mltCodeStr = mcode.ToString();
                    else if (mltMap.TryGetValue(ccode2 + "|" + rawName, out mcode2)) mltCodeStr = mcode2.ToString();
                }
                _persons[key] = mltCodeStr;
            }
        }

        // ALL source lines: item total (qty x nrate) + line tax include blank-item lines; only item lines become sal_order_det.
        // decimal[] = { item, qty, rate, discount, line ncode, taxset, taxamt }
        static void LoadHeaderProducts()
        {
            foreach (DataRow r in GetSrc("SELECT nofficeid, nsales, ncode, nitem, nquantity, nrate, ndiscount, ntaxset, ntaxamt FROM trdsales1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                int office = Int(r[0]);
                int hdr = Int(r[1]); if (hdr <= 0) continue;
                string key = office + "|" + hdr;
                decimal cur;
                _itemsAllByKey.TryGetValue(key, out cur); _itemsAllByKey[key] = cur + Dec(r[4]) * Dec(r[5]);
                _lineTaxByKey.TryGetValue(key, out cur); _lineTaxByKey[key] = cur + Dec(r[8]);
                int item = Int(r[3]); if (item <= 0) continue;
                List<decimal[]> list;
                if (!_hdrProductsByKey.TryGetValue(key, out list)) { list = new List<decimal[]>(); _hdrProductsByKey[key] = list; }
                list.Add(new decimal[] { item, Dec(r[4]), Dec(r[5]), Dec(r[6]), Int(r[2]), Int(r[7]), Dec(r[8]) });
            }
        }

        static void LoadTaxMaps(SqlConnection tgt)
        {
            _taxC = TaxCode(tgt, "CGST"); _taxS = TaxCode(tgt, "SGST"); _taxI = TaxCode(tgt, "IGST");
            _taxsetName = SrcLookupInt("SELECT ncode, vname FROM msttaxset WITH (NOLOCK)");
            foreach (DataRow r in GetTgt("SELECT code, type FROM product").Tables[0].Rows) _prodType[Int(r["code"])] = Str(r["type"]).ToLowerInvariant();
            foreach (DataRow r in GetTgt(@"SELECT m.code, ISNULL(s.stategstcode,'') AS g, s.code AS st FROM mltaddress m
                                           JOIN city c ON c.code = m.citycode JOIN state s ON s.code = c.statecode").Tables[0].Rows)
                _custGstByBranch[Int(r["code"])] = new object[] { Str(r["g"]), Int(r["st"]) };
            Console.WriteLine("  Tax: CGST=" + _taxC + " SGST=" + _taxS + " IGST=" + _taxI + ", branches with state=" + _custGstByBranch.Count + ", home GST state=" + GstHomeGstCode);
        }

        // eBizWiz GST = "post tax charges" (trdsales4posttaxchgs): AMOUNT (namount) or PERCENTAGE (ntaxpercentage, % always on item total).
        // EdifyBiz sales view: line tax = (line value - discount) x sal_tax %, CGST+SGST vs IGST by branch/customer GST state; sal_adjust
        // added after tax. Rules (record-checked 2026-09-15) - Aftab Alam:
        //  * GST % = main GST charge (IGST x | CGST x + SGST y | GST x; "NIL" / above 28% labels = 0); no header GST -> line Tax Set.
        //  * Tax type follows the view's state rule (same state -> CGST/2 + SGST/2, else IGST) so the invoice always shows its GST.
        //  * GST entered as AMOUNT on (items - earlier AMOUNT discount [+ freight]) -> that discount is a pre-GST discount:
        //    spread onto the lines (sal_order_det.discount) instead of an adjustment, so line tax = eBizWiz GST.
        //  * GST the lines can't carry -> "GST on charges (eBizWiz)" (freight taxed), "GST rounding (eBizWiz)" (up to 1),
        //    else "GST difference (eBizWiz)". Grand total = eBizWiz total.
        // returns { sal_tax rows, sal_adjust rows, 1 if a GST difference (not rounding / charges) line was added }
        static int[] ApplyTax(SqlConnection tgt, SqlTransaction tx, int salcode, string key, object billBranch,
                             List<object[]> dets, Dictionary<int, decimal> prodLineRate, object createdon, object updatedon)
        {
            decimal itemsAll, lineTax;
            _itemsAllByKey.TryGetValue(key, out itemsAll);
            _lineTaxByKey.TryGetValue(key, out lineTax);
            List<object[]> charges;
            if (!_chargesByKey.TryGetValue(key, out charges)) charges = new List<object[]>();

            string custGst = ""; int custState = 0; object[] cs;
            if (billBranch != DBNull.Value && _custGstByBranch.TryGetValue(Convert.ToInt32(billBranch), out cs)) { custGst = (string)cs[0]; custState = (int)cs[1]; }
            bool same = custGst.Length > 0 && custGst == GstHomeGstCode;   // mirrors the view: company GST state code == customer GST state code

            List<object[]> adj = new List<object[]>();   // { name, percent, amount }
            decimal viewItems = 0m; foreach (object[] d in dets) viewItems += (decimal)d[2];
            decimal otherItems = Math.Round(itemsAll - viewItems, 2);
            if (otherItems != 0) adj.Add(new object[] { "Other items (eBizWiz)", 0m, otherItems });   // blank-item / unmapped lines

            decimal gstMain = 0m, priorNeg = 0m, priorPos = 0m;
            bool gstPctMode = false, gstAmtMode = false, seenGst = false;
            List<object[]> preNegRows = new List<object[]>();
            decimal amtI = -1, rI = 0, amtC = -1, rC = 0, amtS = -1, rS = 0, amtG = -1, rG = 0;
            foreach (object[] ch in charges)
            {
                string label = (string)ch[0]; bool pctMode = (string)ch[1] == "PERCENTAGE"; decimal pct = (decimal)ch[2];
                decimal amt = pctMode ? Math.Round(itemsAll * pct / 100m, 2) : (decimal)ch[3];
                bool isGst = label.IndexOf("GST", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isGst || label.IndexOf(" on ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (amt == 0) continue;
                    object[] row = new object[] { label, pctMode ? pct : 0m, amt };
                    adj.Add(row);
                    if (!isGst && !seenGst && !pctMode) { if (amt < 0) { priorNeg += amt; preNegRows.Add(row); } else priorPos += amt; }
                    continue;
                }
                decimal rate = pctMode && pct > 0 && pct <= 28 ? pct : GstRateIn(label);
                if (amt == 0 && rate <= 0) continue;
                seenGst = true;
                gstMain += amt;
                if (pctMode) gstPctMode = true; else if (amt != 0) gstAmtMode = true;
                if (rate <= 0) continue;
                if (label.IndexOf("IGST", StringComparison.OrdinalIgnoreCase) >= 0) { if (amt > amtI) { amtI = amt; rI = rate; } }
                else if (label.IndexOf("CGST", StringComparison.OrdinalIgnoreCase) >= 0) { if (amt > amtC) { amtC = amt; rC = rate; } }
                else if (label.IndexOf("SGST", StringComparison.OrdinalIgnoreCase) >= 0) { if (amt > amtS) { amtS = amt; rS = rate; } }
                else { if (amt > amtG) { amtG = amt; rG = rate; } }
            }
            decimal R = rI > 0 ? rI : (rC + rS > 0 ? rC + rS : rG);
            if (gstMain == 0) R = 0m;   // GST label present but no amount charged (e.g. SA1792) -> no GST on the lines

            // Pre-GST AMOUNT discount -> line discount, when eBizWiz's GST amount was calculated on items - that discount (+ freight).
            decimal lineDisc = 0m; bool chargesTaxed = false;
            if (R > 0 && gstAmtMode && !gstPctMode)
            {
                decimal tol = 2m + Math.Abs(gstMain) * 0.005m;
                if (priorNeg < 0 && Math.Abs(gstMain - (itemsAll + priorNeg) * R / 100m) <= tol) lineDisc = -priorNeg;
                else if (priorNeg < 0 && priorPos > 0 && Math.Abs(gstMain - (itemsAll + priorNeg + priorPos) * R / 100m) <= tol) { lineDisc = -priorNeg; chargesTaxed = true; }
                else if (priorPos > 0 && Math.Abs(gstMain - (itemsAll + priorPos) * R / 100m) <= tol) chargesTaxed = true;
            }
            if (lineDisc > 0 && lineDisc < viewItems)
            {
                foreach (object[] row in preNegRows) adj.Remove(row);
                decimal left = lineDisc; int lastPos = -1;
                for (int k = 0; k < dets.Count; k++) if ((decimal)dets[k][2] > 0) lastPos = k;
                for (int k = 0; k < dets.Count; k++)
                {
                    decimal v = (decimal)dets[k][2]; if (v <= 0) continue;
                    decimal share = k == lastPos ? left : Math.Round(lineDisc * v / viewItems, 2);
                    left -= share;
                    dets[k][2] = v - share;
                    using (SqlCommand u = new SqlCommand("UPDATE sal_order_det SET discount=@d WHERE code=@c", tgt, tx))
                    { u.Parameters.AddWithValue("@d", share); u.Parameters.AddWithValue("@c", (int)dets[k][0]); u.ExecuteNonQuery(); }
                }
                _invLineDisc++;
            }

            // sal_tax per product (view joins by product + tax), rows by the view's state rule.
            List<int> prodOrder = new List<int>(); Dictionary<int, decimal> prodVal = new Dictionary<int, decimal>();
            foreach (object[] d in dets) { int pc = (int)d[1]; if (!prodVal.ContainsKey(pc)) { prodOrder.Add(pc); prodVal[pc] = 0m; } prodVal[pc] += (decimal)d[2]; }
            Dictionary<int, decimal> prodRate = new Dictionary<int, decimal>();
            int rows = 0;
            foreach (int pc in prodOrder)
            {
                decimal lr, rate = R > 0 ? R : (prodLineRate.TryGetValue(pc, out lr) ? lr : 0m);
                if (rate <= 0) continue;
                prodRate[pc] = rate;
                decimal v = prodVal[pc];
                if (same)
                {
                    InsertTax(tgt, tx, salcode, pc, _taxC, rate / 2m, Math.Round(v * rate / 200m, 2), custState, createdon, updatedon);
                    InsertTax(tgt, tx, salcode, pc, _taxS, rate / 2m, Math.Round(v * rate / 200m, 2), custState, createdon, updatedon);
                    rows += 2;
                }
                else { InsertTax(tgt, tx, salcode, pc, _taxI, rate, Math.Round(v * rate / 100m, 2), custState, createdon, updatedon); rows++; }
                using (SqlCommand u = new SqlCommand("UPDATE sal_order_det SET gst=@g WHERE salcode=@sc AND prodcode=@p", tgt, tx))
                { u.Parameters.AddWithValue("@g", rate); u.Parameters.AddWithValue("@sc", salcode); u.Parameters.AddWithValue("@p", pc); u.ExecuteNonQuery(); }
            }
            decimal viewTax = 0m;   // per line, rounded like sales.js
            foreach (object[] d in dets)
            {
                decimal rate; if (!prodRate.TryGetValue((int)d[1], out rate)) continue;
                decimal v = (decimal)d[2];
                viewTax += same ? Math.Round(v * rate / 200m, 2) * 2m : Math.Round(v * rate / 100m, 2);
            }

            decimal gstDiff = Math.Round(gstMain + lineTax - viewTax, 2);
            bool realDiff = false;
            if (gstDiff != 0)
            {
                string nm = chargesTaxed ? "GST on charges (eBizWiz)" : (Math.Abs(gstDiff) <= 1m ? "GST rounding (eBizWiz)" : "GST difference (eBizWiz)");
                realDiff = nm == "GST difference (eBizWiz)";
                adj.Add(new object[] { nm, 0m, gstDiff });
            }

            int adjN = 0;
            foreach (object[] a in adj)
            {
                using (SqlCommand q = new SqlCommand(@"INSERT INTO sal_adjust (salcode, adjustname, adjustpercent, adjustamount, beforetax, createdby, updatedby, createdon, updatedon)
                                                     VALUES (@sc, @n, @p, @a, 0, @cb, @cb, @con, @uon)", tgt, tx))
                {
                    q.Parameters.AddWithValue("@sc", salcode);
                    q.Parameters.AddWithValue("@n", Cap((string)a[0], 100));   // adjustname widened to varchar(100) in 00_ALTERS
                    q.Parameters.AddWithValue("@p", a[1]);
                    q.Parameters.AddWithValue("@a", a[2]);
                    q.Parameters.AddWithValue("@cb", MigrationUser);
                    q.Parameters.AddWithValue("@con", createdon);
                    q.Parameters.AddWithValue("@uon", updatedon);
                    q.ExecuteNonQuery();
                }
                adjN++;
            }
            return new int[] { rows, adjN, realDiff ? 1 : 0 };
        }

        static void InsertTax(SqlConnection tgt, SqlTransaction tx, int salcode, int pcode, int taxcode, decimal pct, decimal amt, int custState, object createdon, object updatedon)
        {
            using (SqlCommand q = new SqlCommand(@"INSERT INTO sal_tax (sal_code, pcode, taxcode, taxpercent, taxstatefrom, taxstateto, taxamount, beforetax, createdby, updatedby, createdon, updatedon)
                                                 VALUES (@sc, @p, @t, @pct, @from, @to, @amt, 0, @cb, @cb, @con, @uon)", tgt, tx))
            {
                q.Parameters.AddWithValue("@sc", salcode);
                q.Parameters.AddWithValue("@p", pcode);
                q.Parameters.AddWithValue("@t", taxcode);
                q.Parameters.AddWithValue("@pct", pct);
                q.Parameters.AddWithValue("@from", GstHomeStateCode);
                q.Parameters.AddWithValue("@to", custState);
                q.Parameters.AddWithValue("@amt", amt);
                q.Parameters.AddWithValue("@cb", MigrationUser);
                q.Parameters.AddWithValue("@con", createdon);
                q.Parameters.AddWithValue("@uon", updatedon);
                q.ExecuteNonQuery();
            }
        }

        // Serial / warranty detail (trdsales2itemsdet, per line via nsales1 = trdsales1items.ncode) -> aggregate per line.
        // Reuses sal_order_det columns (warrantymonths, installationdt, location) + serials as text in installedremarks;
        // no serial master, no ALTER. object[] = { serialsCsv, warrantymonths, installdate, location }. - Aftab Alam
        static void LoadSerialWarranty()
        {
            foreach (DataRow r in GetSrc("SELECT nofficeid, nsales1, vserialno, nmonths, dinstdate, vlocation FROM trdsales2itemsdet WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nsales1, ncode").Tables[0].Rows)
            {
                int lineNcode = Int(r["nsales1"]); if (lineNcode <= 0) continue;
                string key = Int(r["nofficeid"]) + "|" + lineNcode;
                object[] agg;
                if (!_serialByLine.TryGetValue(key, out agg)) { agg = new object[] { "", DBNull.Value, DBNull.Value, DBNull.Value }; _serialByLine[key] = agg; }
                string sn = Str(r["vserialno"]).Trim();
                if (sn.Length > 0) agg[0] = ((string)agg[0]).Length > 0 ? (string)agg[0] + ", " + sn : sn;
                if (agg[1] == DBNull.Value && Int(r["nmonths"]) > 0) agg[1] = Int(r["nmonths"]);
                if (agg[2] == DBNull.Value && r["dinstdate"] != DBNull.Value) agg[2] = r["dinstdate"];
                if (agg[3] == DBNull.Value && Str(r["vlocation"]).Trim().Length > 0) agg[3] = Str(r["vlocation"]).Trim();
            }
        }

        // Post-tax charges in entry order (ncode). Label rows (TOTAL ..., amount/percent empty) come through with 0 and are skipped later.
        static void LoadCharges()
        {
            int n = 0;
            foreach (DataRow r in GetSrc(@"SELECT c.nofficeid, c.nsales, p.vname, c.venteredinamtorper, c.ntaxpercentage, c.namount, c.bapplyonitemtotalonly
                                           FROM trdsales4posttaxchgs c WITH (NOLOCK) JOIN mstprepostchgs p WITH (NOLOCK) ON p.ncode = c.nmstposttaxchgs
                                           WHERE c.nofficeid IN (" + OfficeIn + ") ORDER BY c.nofficeid, c.nsales, c.ncode").Tables[0].Rows)
            {
                string key = Int(r["nofficeid"]) + "|" + Int(r["nsales"]);
                List<object[]> list;
                if (!_chargesByKey.TryGetValue(key, out list)) { list = new List<object[]>(); _chargesByKey[key] = list; }
                list.Add(new object[] { Str(r["vname"]).Trim(), Str(r["venteredinamtorper"]).ToUpperInvariant(), Dec(r["ntaxpercentage"]), Dec(r["namount"]), Bool(r["bapplyonitemtotalonly"]) });
                n++;
            }
            Console.WriteLine("  Post-tax charges loaded: " + n + " across " + _chargesByKey.Count + " sales");
        }

        static void LoadChecklists()
        {
            foreach (DataRow dr in GetSrc("SELECT ncode, vname FROM mstchecks WITH (NOLOCK)").Tables[0].Rows)
                _checkName[Int(dr["ncode"]).ToString()] = Str(dr["vname"]);
            int rows = 0;
            foreach (DataRow dr in GetSrc("SELECT nofficeid, nsales, ncheck, dexpdate, bdone, ddonedate, nallotedto, vremarks, addedon, editedon FROM trdsales6checks WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nsales, ncode").Tables[0].Rows)
            {
                string key = Int(dr["nofficeid"]) + "|" + Int(dr["nsales"]);
                List<object[]> list;
                if (!_checklistsByKey.TryGetValue(key, out list)) { list = new List<object[]>(); _checklistsByKey[key] = list; }
                list.Add(new object[]
                {
                    Int(dr["ncheck"]),      // 0 check master ncode
                    P(dr["dexpdate"]),      // 1 expected date
                    Bool(dr["bdone"]),      // 2 done
                    P(dr["ddonedate"]),     // 3 done date
                    NKey(dr["nallotedto"]), // 4 alloted-to source user NKey
                    Str(dr["vremarks"]),    // 5 remarks
                    P(dr["addedon"]),       // 6 added on
                    P(dr["editedon"]),      // 7 edited on
                });
                rows++;
            }
            Console.WriteLine("  Check lists loaded: " + rows + " items across " + _checklistsByKey.Count + " sales");
        }

        static void BuildInqcssoLinks(SqlConnection tgt)
        {
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhordrc WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["ncode"]);
                if (!_soRefByOfficeHdr.ContainsKey(k)) _soRefByOfficeHdr[k] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhquote WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["ncode"]);
                if (!_cqRefByOfficeHdr.ContainsKey(k)) _cqRefByOfficeHdr[k] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            }
            foreach (DataRow r in GetTgt("SELECT code, branchcode, inqref, cors FROM inqcs WITH (NOLOCK) WHERE cors IN ('SO','CQ') AND branchcode IS NOT NULL AND inqref IS NOT NULL").Tables[0].Rows)
            {
                string k = Int(r["branchcode"]) + "|" + Str(r["inqref"]).Trim();
                if (Str(r["cors"]) == "SO") { if (!_soCodeByKey.ContainsKey(k)) _soCodeByKey[k] = Int(r["code"]); }
                else { if (!_cqCodeByKey.ContainsKey(k)) _cqCodeByKey[k] = Int(r["code"]); }
            }
        }

        static string Join(string a, string b) { return string.Join(" ", new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(); }
    }

    // ==================================================================
    //  STOCK : trhstkin/trdstkin1items + trhstkou/trdstkou1items -> EdifyBiz Inventory (no form changes)
    //  Stock-IN party/user/unpaired office -> GRN (stockinout type GRN); Stock-OUT party/user -> MRO (type MRO);
    //  office OUT <-> IN pairs with the same items -> GTA (stockb2b, received); OUT to office with no IN -> GTA in transit.
    //  Ledger stocktrans like app/inventory/inventoryfunction.asp InventoryTrigger (modulecode = line code), plus SAL
    //  (stock sold) for migrated Sales Invoice lines, then productstocksummary rebuilt from stocktrans.
    //  Mapping: Maping\10_stock_field_mapping.md. Run AFTER Purchase Order + Sales Invoice. - Aftab Alam
    // ==================================================================
    static class Stock
    {
        static Dictionary<int, string> _fixed, _officeName;
        static Dictionary<string, string> _userName;
        static readonly Dictionary<int, int> _branchByOffice = new Dictionary<int, int>();
        static readonly Dictionary<string, int> _custByParty = new Dictionary<string, int>();
        static readonly Dictionary<int, int> _prodByItem = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _unitByProd = new Dictionary<int, int>();
        static readonly Dictionary<int, string> _typeByProd = new Dictionary<int, string>();
        static Dictionary<int, int> _batchByProd;
        static readonly Dictionary<string, int> _poByLine = new Dictionary<string, int>();        // office|trdordpl1items.ncode -> pur_order.code
        static readonly Dictionary<string, string> _poNoByLine = new Dictionary<string, string>(); // office|trdordpl1items.ncode -> PO no
        static readonly Dictionary<string, List<object[]>> _inLines = new Dictionary<string, List<object[]>>();   // office|stkin -> { item, accept, reject, missing, rate, nordpl1 }
        static readonly Dictionary<string, List<object[]>> _outLines = new Dictionary<string, List<object[]>>();  // office|stkou -> { item, qty, def, rate, nordrc1 }
        static readonly Dictionary<string, List<string>> _inCharges = new Dictionary<string, List<string>>();
        static readonly Dictionary<string, List<string>> _outCharges = new Dictionary<string, List<string>>();
        static readonly Dictionary<string, List<string>> _serials = new Dictionary<string, List<string>>();       // office|stkin -> serial texts
        static int _grn, _mro, _gta, _gtaTransit, _lines, _skipLines, _errors, _sal, _pairsSplit, _poLinked;

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: Stock (GRN / MRO / GTA + stock ledger)");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            _fixed = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
            _userName = SrcLookupNKey("SELECT ncode, vname FROM mstusers WITH (NOLOCK)");

            using (SqlConnection tgt = OpenTgt())
            {
                BuildMaps(tgt);
                SeedGrnTypes(tgt);
                LoadSource();

                DataTable inH = GetSrc(@"SELECT ncode, vtrnprefix, ntrnno, dtrndate, nstkinfrom, nstkintype, nfromoffice, nfromparty, nfromuser, nstkouno,
                                                vrefno, drefdate, vremarks, vcomment, ndispatchmode, vdespatchthru, vdespdocno, ddespdocdate, nofficeid, addedon, editedon
                                         FROM trhstkin WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY dtrndate, ncode").Tables[0];
                DataTable outH = GetSrc(@"SELECT ncode, vtrnprefix, ntrnno, dtrndate, nstkoutto, nstkoutype, ntooffice, ntoparty, ntouser, ncalls,
                                                 vrefno, drefdate, vremarks, vcomment, ndispatchmode, vdespatchthru, vdespdocno, ddespdocdate, nofficeid, addedon, editedon
                                          FROM trhstkou WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY dtrndate, ncode").Tables[0];

                // Office transfer pairs: Stock-IN (nstkouno) <-> Stock-OUT at the sending office. Same items (OUT qty/def = IN accept/reject) -> one GTA.
                Dictionary<string, DataRow> outByKey = new Dictionary<string, DataRow>();
                foreach (DataRow o in outH.Rows) outByKey[Int(o["nofficeid"]) + "|" + Int(o["ncode"])] = o;
                Dictionary<string, DataRow> pairInByOut = new Dictionary<string, DataRow>();
                HashSet<string> pairedIn = new HashSet<string>();
                foreach (DataRow i in inH.Rows)
                {
                    int from = Int(i["nfromoffice"]), outNo = Int(i["nstkouno"]);
                    if (from <= 0 || outNo <= 0) continue;
                    DataRow o; string ok = from + "|" + outNo;
                    if (!outByKey.TryGetValue(ok, out o) || Int(o["ntooffice"]) != Int(i["nofficeid"]) || pairInByOut.ContainsKey(ok)) continue;
                    if (SameItems(ok, Int(i["nofficeid"]) + "|" + Int(i["ncode"]))) { pairInByOut[ok] = i; pairedIn.Add(Int(i["nofficeid"]) + "|" + Int(i["ncode"])); }
                    else _pairsSplit++;
                }
                Console.WriteLine("  Office transfer pairs: " + pairInByOut.Count + " as GTA, " + _pairsSplit + " with different items -> MRO + GRN");
                Console.WriteLine("-----------------------------------------------------");

                foreach (DataRow o in outH.Rows)
                {
                    string key = Int(o["nofficeid"]) + "|" + Int(o["ncode"]);
                    try
                    {
                        DataRow pin;
                        if (Int(o["ntooffice"]) > 0 && pairInByOut.TryGetValue(key, out pin)) InsertGta(tgt, o, pin);
                        else if (Int(o["ntooffice"]) > 0 && !HasInFor(inH, o)) InsertGta(tgt, o, null);   // sent, never received -> in transit
                        else InsertMro(tgt, o);
                    }
                    catch (Exception ex) { _errors++; if (_errors <= 20) Console.WriteLine("  [ERROR] Stock-OUT " + key + ": " + ex.Message); }
                }
                foreach (DataRow i in inH.Rows)
                {
                    string key = Int(i["nofficeid"]) + "|" + Int(i["ncode"]);
                    if (pairedIn.Contains(key)) continue;
                    try { InsertGrn(tgt, i); }
                    catch (Exception ex) { _errors++; if (_errors <= 20) Console.WriteLine("  [ERROR] Stock-IN " + key + ": " + ex.Message); }
                }

                PostSales(tgt);
                RebuildSummary(tgt);

                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("  GRN (Stock-IN)          : " + _grn + "  (PO linked " + _poLinked + ")");
                Console.WriteLine("  MRO (Stock-OUT)         : " + _mro);
                Console.WriteLine("  GTA (office transfer)   : " + _gta + "  (in transit " + _gtaTransit + ")");
                Console.WriteLine("  Lines                   : " + _lines + "  (skipped, item not migrated: " + _skipLines + ")");
                Console.WriteLine("  SAL stock-out (invoices): " + _sal + " lines");
                Console.WriteLine("  Errors                  : " + _errors);
                Console.WriteLine("-----------------------------------------------------");
            }
        }

        static bool HasInFor(DataTable inH, DataRow o)
        {
            foreach (DataRow i in inH.Rows)
                if (Int(i["nstkouno"]) == Int(o["ncode"]) && Int(i["nfromoffice"]) == Int(o["nofficeid"]) && Int(i["nofficeid"]) == Int(o["ntooffice"])) return true;
            return false;
        }

        static bool SameItems(string outKey, string inKey)
        {
            Dictionary<int, decimal[]> a = new Dictionary<int, decimal[]>(), b = new Dictionary<int, decimal[]>();
            List<object[]> ol, il;
            if (_outLines.TryGetValue(outKey, out ol)) foreach (object[] l in ol) { decimal[] v; if (!a.TryGetValue((int)l[0], out v)) a[(int)l[0]] = v = new decimal[2]; v[0] += (decimal)l[1]; v[1] += (decimal)l[2]; }
            if (_inLines.TryGetValue(inKey, out il)) foreach (object[] l in il) { decimal[] v; if (!b.TryGetValue((int)l[0], out v)) b[(int)l[0]] = v = new decimal[2]; v[0] += (decimal)l[1]; v[1] += (decimal)l[2]; }
            if (a.Count == 0 || a.Count != b.Count) return false;
            foreach (KeyValuePair<int, decimal[]> kv in a) { decimal[] v; if (!b.TryGetValue(kv.Key, out v) || v[0] != kv.Value[0] || v[1] != kv.Value[1]) return false; }
            return true;
        }

        // ---------- GRN ----------
        static void InsertGrn(SqlConnection tgt, DataRow h)
        {
            int office = Int(h["nofficeid"]); string key = office + "|" + Int(h["ncode"]);
            int branch; if (!_branchByOffice.TryGetValue(office, out branch)) throw new Exception("office branch not found");
            object date = DocDate(h);
            string from = LookInt(_fixed, Int(h["nstkinfrom"]));
            object scode = DBNull.Value; int cc;
            if (Int(h["nstkinfrom"]) > 0 && NKey(h["nfromparty"]).Length > 0 && _custByParty.TryGetValue(NKey(h["nfromparty"]), out cc)) scode = cc;

            List<object[]> lines; _inLines.TryGetValue(key, out lines);
            object module = DBNull.Value, modulecode = DBNull.Value; List<string> poNos = new List<string>();
            if (lines != null)
                foreach (object[] l in lines)
                {
                    string lk = office + "|" + (int)l[5]; int po; string pno;
                    if ((int)l[5] > 0 && _poByLine.TryGetValue(lk, out po)) { if (modulecode == DBNull.Value) { module = "PO"; modulecode = po; } }
                    if ((int)l[5] > 0 && _poNoByLine.TryGetValue(lk, out pno) && !poNos.Contains(pno)) poNos.Add(pno);
                }

            List<string> rem = BaseRemark(h);
            rem.Insert(0, "Stock-IN " + from + (Int(h["nfromoffice"]) > 0 ? " " + LookInt(_officeName, Int(h["nfromoffice"])) : "")
                          + (NKey(h["nfromuser"]).Length > 0 ? " User: " + LookStr(_userName, NKey(h["nfromuser"])) : ""));
            if (poNos.Count > 1) rem.Add("Against POs: " + string.Join(", ", poNos));
            decimal missing = 0m; if (lines != null) foreach (object[] l in lines) missing += (decimal)l[3];
            if (missing != 0) rem.Add("Missing qty: " + missing.ToString("0.##", CultureInfo.InvariantCulture));
            List<string> ch; if (_inCharges.TryGetValue(key, out ch)) rem.Add("Charges: " + string.Join("; ", ch));
            List<string> sr; if (_serials.TryGetValue(key, out sr)) rem.Add("Serial No: " + string.Join(", ", sr));

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                try
                {
                    int code = InsertStockInOut(tgt, tx, "GRN", h, date, branch, scode, module, modulecode, LookInt(_fixed, Int(h["nstkintype"])), rem);
                    if (lines != null)
                        foreach (object[] l in lines)
                        {
                            int pc; if (!_prodByItem.TryGetValue((int)l[0], out pc)) { _skipLines++; continue; }
                            decimal q = (decimal)l[1], dq = (decimal)l[2];
                            int det = InsertDet(tgt, tx, "stockinoutdet", "stockinoutcode", code, pc, q, dq, (decimal)l[4], date);
                            Ledger(tgt, tx, date, branch, pc, q, dq, "GRN", det);
                            _lines++;
                        }
                    tx.Commit(); _grn++; if (modulecode != DBNull.Value) _poLinked++;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // ---------- MRO ----------
        static void InsertMro(SqlConnection tgt, DataRow h)
        {
            int office = Int(h["nofficeid"]); string key = office + "|" + Int(h["ncode"]);
            int branch; if (!_branchByOffice.TryGetValue(office, out branch)) throw new Exception("office branch not found");
            object date = DocDate(h);
            object scode = DBNull.Value; int cc;
            if (NKey(h["ntoparty"]).Length > 0 && _custByParty.TryGetValue(NKey(h["ntoparty"]), out cc)) scode = cc;
            List<string> rem = BaseRemark(h);
            rem.Insert(0, "Stock-OUT " + LookInt(_fixed, Int(h["nstkoutto"])) + " / " + LookInt(_fixed, Int(h["nstkoutype"]))
                          + (Int(h["ntooffice"]) > 0 ? " " + LookInt(_officeName, Int(h["ntooffice"])) : "")
                          + (NKey(h["ntouser"]).Length > 0 ? " User: " + LookStr(_userName, NKey(h["ntouser"])) : ""));
            if (Int(h["ncalls"]) > 0) rem.Add("Call ref: " + Int(h["ncalls"]));
            List<string> ch; if (_outCharges.TryGetValue(key, out ch)) rem.Add("Charges: " + string.Join("; ", ch));
            List<object[]> lines; _outLines.TryGetValue(key, out lines);

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                try
                {
                    int code = InsertStockInOut(tgt, tx, "MRO", h, date, branch, scode, DBNull.Value, DBNull.Value, null, rem);
                    if (lines != null)
                        foreach (object[] l in lines)
                        {
                            int pc; if (!_prodByItem.TryGetValue((int)l[0], out pc)) { _skipLines++; continue; }
                            decimal q = (decimal)l[1], dq = (decimal)l[2];
                            int det = InsertDet(tgt, tx, "stockinoutdet", "stockinoutcode", code, pc, q, dq, (decimal)l[3], date);
                            Ledger(tgt, tx, date, branch, pc, -q, -dq, "MRO", det);   // good and defective stock both leave the branch
                            _lines++;
                        }
                    tx.Commit(); _mro++;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // ---------- GTA ----------
        static void InsertGta(SqlConnection tgt, DataRow o, DataRow pairIn)
        {
            int office = Int(o["nofficeid"]); string key = office + "|" + Int(o["ncode"]);
            int fromBr, toBr;
            if (!_branchByOffice.TryGetValue(office, out fromBr) || !_branchByOffice.TryGetValue(Int(o["ntooffice"]), out toBr)) throw new Exception("office branch not found");
            object date = DocDate(o);
            bool transit = pairIn == null;
            List<string> rem = BaseRemark(o);
            rem.Insert(0, transit ? "Stock-OUT to office - no Stock-IN found in eBizWiz (in transit)"
                                  : "Received by Stock-IN " + (Str(pairIn["vtrnprefix"]) + Str(pairIn["ntrnno"])).Trim() + " on " + Convert.ToDateTime(DocDate(pairIn)).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            List<object[]> lines; _outLines.TryGetValue(key, out lines);

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                try
                {
                    int code;
                    using (SqlCommand c = new SqlCommand(@"INSERT INTO stockb2b (number, date, frombranchcode, tobranchcode, remark, intransit, createdby, createdon, updatedby, updatedon)
                                                         OUTPUT INSERTED.code VALUES (@no, @dt, @fb, @tb, @rem, @it, @cb, @dt, @cb, @dt)", tgt, tx))
                    {
                        c.Parameters.AddWithValue("@no", Cap((Str(o["vtrnprefix"]) + Str(o["ntrnno"])).Trim(), 30));
                        c.Parameters.AddWithValue("@dt", date);
                        c.Parameters.AddWithValue("@fb", fromBr);
                        c.Parameters.AddWithValue("@tb", toBr);
                        c.Parameters.AddWithValue("@rem", PS(string.Join(" | ", rem)));
                        c.Parameters.AddWithValue("@it", transit ? 1 : 0);
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        code = Convert.ToInt32(c.ExecuteScalar());
                    }
                    if (lines != null)
                        foreach (object[] l in lines)
                        {
                            int pc; if (!_prodByItem.TryGetValue((int)l[0], out pc)) { _skipLines++; continue; }
                            decimal q = (decimal)l[1], dq = (decimal)l[2];
                            int det = InsertDet(tgt, tx, "stockb2bdet", "stockb2bcode", code, pc, q, dq, (decimal)l[3], date);
                            Ledger(tgt, tx, date, fromBr, pc, -q, -dq, "GTA", det);
                            Ledger(tgt, tx, date, transit ? 0 : toBr, pc, q, dq, "GTA", det);
                            _lines++;
                        }
                    tx.Commit(); _gta++; if (transit) _gtaTransit++;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // ---------- shared inserts ----------
        static int InsertStockInOut(SqlConnection tgt, SqlTransaction tx, string type, DataRow h, object date, int branch, object scode, object module, object modulecode, string grntype, List<string> rem)
        {
            using (SqlCommand c = new SqlCommand(@"INSERT INTO stockinout (number, date, module, modulecode, branchcode, type, remark, scode, grntype, createdby, createdon, updatedby, updatedon)
                                                 OUTPUT INSERTED.code VALUES (@no, @dt, @mod, @mc, @br, @type, @rem, @sc, @gt, @cb, @dt, @cb, @dt)", tgt, tx))
            {
                c.Parameters.AddWithValue("@no", Cap((Str(h["vtrnprefix"]) + Str(h["ntrnno"])).Trim(), 30));
                c.Parameters.AddWithValue("@dt", date);
                c.Parameters.AddWithValue("@mod", module);
                c.Parameters.AddWithValue("@mc", modulecode);
                c.Parameters.AddWithValue("@br", branch);
                c.Parameters.AddWithValue("@type", type);
                c.Parameters.AddWithValue("@rem", PS(string.Join(" | ", rem)));
                c.Parameters.AddWithValue("@sc", scode);
                c.Parameters.AddWithValue("@gt", PS(Cap(grntype, 100)));
                c.Parameters.AddWithValue("@cb", MigrationUser);
                return Convert.ToInt32(c.ExecuteScalar());
            }
        }

        static int InsertDet(SqlConnection tgt, SqlTransaction tx, string table, string parentCol, int parent, int pc, decimal qty, decimal dqty, decimal price, object date)
        {
            int bt; object batch = _batchByProd.TryGetValue(pc, out bt) ? (object)bt : DBNull.Value;
            int u; object unit = _unitByProd.TryGetValue(pc, out u) && u > 0 ? (object)u : DBNull.Value;
            using (SqlCommand d = new SqlCommand("INSERT INTO " + table + " (" + parentCol + ", productcode, prodbatchcode, qty, dqty, unit, price, createdby, createdon, updatedby, updatedon) " +
                                                 "OUTPUT INSERTED.code VALUES (@p, @pc, @b, @q, @dq, @u, @pr, @cb, @dt, @cb, @dt)", tgt, tx))
            {
                d.Parameters.AddWithValue("@p", parent);
                d.Parameters.AddWithValue("@pc", pc);
                d.Parameters.AddWithValue("@b", batch);
                d.Parameters.AddWithValue("@q", qty);
                d.Parameters.AddWithValue("@dq", dqty);
                d.Parameters.AddWithValue("@u", unit);
                d.Parameters.AddWithValue("@pr", price);
                d.Parameters.AddWithValue("@cb", MigrationUser);
                d.Parameters.AddWithValue("@dt", date);
                return Convert.ToInt32(d.ExecuteScalar());
            }
        }

        // stocktrans row: date + createdon = document date (Stock Summary filters on createdon), modulecode = line code.
        static void Ledger(SqlConnection tgt, SqlTransaction tx, object date, int branch, int pc, decimal qty, decimal dqty, string module, int det)
        {
            int bt; object batch = _batchByProd.TryGetValue(pc, out bt) ? (object)bt : DBNull.Value;
            int u; object unit = _unitByProd.TryGetValue(pc, out u) && u > 0 ? (object)u : DBNull.Value;
            using (SqlCommand s = new SqlCommand(@"INSERT INTO stocktrans (date, branchcode, productcode, prodbatchcode, qty, dqty, unit, module, modulecode, createdby, createdon, updatedby, updatedon)
                                                 VALUES (@dt, @br, @pc, @b, @q, @dq, @u, @m, @mc, @cb, @dt, @cb, @dt)", tgt, tx))
            {
                s.Parameters.AddWithValue("@dt", date);
                s.Parameters.AddWithValue("@br", branch);
                s.Parameters.AddWithValue("@pc", pc);
                s.Parameters.AddWithValue("@b", batch);
                s.Parameters.AddWithValue("@q", qty);
                s.Parameters.AddWithValue("@dq", dqty);
                s.Parameters.AddWithValue("@u", unit);
                s.Parameters.AddWithValue("@m", module);
                s.Parameters.AddWithValue("@mc", det);
                s.Parameters.AddWithValue("@cb", MigrationUser);
                s.ExecuteNonQuery();
            }
        }

        // Stock sold: migrated Sales Invoice lines (non-service products) -> stocktrans SAL -qty at the invoice branch/date,
        // as SalesInventoryTrigger does. Skips lines already posted. eBizWiz deducts sold stock (stock report column "Sold").
        static void PostSales(SqlConnection tgt)
        {
            using (SqlCommand s = new SqlCommand(@"INSERT INTO stocktrans (date, branchcode, productcode, prodbatchcode, qty, dqty, unit, module, modulecode, createdby, createdon, updatedby, updatedon)
                SELECT a.salorderdt, a.branchcode, d.prodcode, d.prodbatchcode, -ISNULL(d.qty,0), 0, p.unitcode, 'SAL', d.code, @cb, a.salorderdt, @cb, a.salorderdt
                FROM sal_order a JOIN sal_order_det d ON d.salcode = a.code JOIN product p ON p.code = d.prodcode
                WHERE a.type = 'SO' AND a.branchcode IN (" + string.Join(",", _branchByOffice.Values) + @")
                  AND ISNULL(p.type,'p') <> 's' AND ISNULL(d.qty,0) <> 0 AND a.salorderdt IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM stocktrans t WHERE t.module = 'SAL' AND t.modulecode = d.code)", tgt))
            {
                s.CommandTimeout = 0;
                s.Parameters.AddWithValue("@cb", MigrationUser);
                s.Parameters.AddWithValue("@cc", CompanyCode);
                _sal = s.ExecuteNonQuery();
            }
        }

        // productstocksummary = sum of stocktrans per branch + product + batch (same values InventoryTrigger keeps).
        static void RebuildSummary(SqlConnection tgt)
        {
            string sql = @"
                UPDATE s SET qty = t.q, dqty = t.dq, updatedon = GETDATE()
                FROM productstocksummary s JOIN (SELECT branchcode, productcode, prodbatchcode, SUM(qty) q, SUM(dqty) dq FROM stocktrans GROUP BY branchcode, productcode, prodbatchcode) t
                  ON t.branchcode = s.branchcode AND t.productcode = s.prodcode AND t.prodbatchcode = s.prodbatchcode;
                INSERT INTO productstocksummary (branchcode, prodcode, prodbatchcode, qty, dqty, asondate, createdby, createdon, updatedby, updatedon)
                SELECT t.branchcode, t.productcode, t.prodbatchcode, SUM(t.qty), SUM(t.dqty), GETDATE(), @cb, GETDATE(), @cb, GETDATE()
                FROM stocktrans t
                WHERE NOT EXISTS (SELECT 1 FROM productstocksummary s WHERE s.branchcode = t.branchcode AND s.prodcode = t.productcode AND s.prodbatchcode = t.prodbatchcode)
                GROUP BY t.branchcode, t.productcode, t.prodbatchcode;";
            using (SqlCommand c = new SqlCommand(sql, tgt)) { c.CommandTimeout = 0; c.Parameters.AddWithValue("@cb", MigrationUser); c.ExecuteNonQuery(); }
            object n = new SqlCommand("SELECT COUNT(*) FROM productstocksummary", tgt).ExecuteScalar();
            Console.WriteLine("  productstocksummary rows: " + Convert.ToInt32(n));
        }

        static object DocDate(DataRow h)
        {
            // SI2384 is dated 2202-11-10 in eBizWiz (typo) -> any date after today uses the entry date instead.
            if (h["dtrndate"] == DBNull.Value || Convert.ToDateTime(h["dtrndate"]) > DateTime.Today.AddYears(1)) return h["addedon"] != DBNull.Value ? h["addedon"] : (object)DateTime.Today;
            return h["dtrndate"];
        }

        static List<string> BaseRemark(DataRow h)
        {
            List<string> p = new List<string>();
            string r = Str(h["vremarks"]); if (r.Length > 0) p.Add(r);
            string c = Str(h["vcomment"]); if (c.Length > 0) p.Add("Comment: " + c);
            string rf = Str(h["vrefno"]); if (rf.Length > 0) p.Add("Ref No: " + rf);
            if (h["drefdate"] != DBNull.Value) p.Add("Ref Date: " + Convert.ToDateTime(h["drefdate"]).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            string dm = LookInt(_fixed, Int(h["ndispatchmode"])); if (dm.Length > 0) p.Add("Dispatch Mode: " + dm);
            string th = Str(h["vdespatchthru"]); if (th.Length > 0) p.Add("Dispatch Through: " + th);
            string dn = Str(h["vdespdocno"]); if (dn.Length > 0) p.Add("Docket No: " + dn);
            if (h["ddespdocdate"] != DBNull.Value) p.Add("Docket Date: " + Convert.ToDateTime(h["ddespdocdate"]).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            return p;
        }

        // ---------- maps / source ----------
        static void BuildMaps(SqlConnection tgt)
        {
            foreach (DataRow r in GetSrc("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK) WHERE ncode IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                using (SqlCommand q = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                {
                    q.Parameters.AddWithValue("@cc", CompanyCode); q.Parameters.AddWithValue("@pl", Cap(Str(r["vcompanyname"]), 100));
                    object o = q.ExecuteScalar(); if (o != null && o != DBNull.Value) _branchByOffice[Int(r["ncode"])] = Convert.ToInt32(o);
                }
            }
            Dictionary<string, int> cByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM contact WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !cByName.ContainsKey(n)) cByName[n] = Int(r["code"]); }
            foreach (DataRow r in GetSrc("SELECT ncode, ntitle, vname FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string k = NKey(r["ncode"]); if (k.Length == 0) continue;
                string nm = Cap(string.Join(" ", new[] { LookInt(_fixed, Int(r["ntitle"])), Str(r["vname"]) }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(), 150);
                int c; if (nm.Length > 0 && cByName.TryGetValue(nm, out c) && !_custByParty.ContainsKey(k)) _custByParty[k] = c;
            }
            foreach (KeyValuePair<string, int> kv in PartyToContact) _custByParty[kv.Key] = kv.Value;

            Dictionary<string, int> pByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name, unitcode, type FROM product WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows)
            {
                string n = Str(r["name"]); int code = Int(r["code"]);
                if (n.Length > 0 && !pByName.ContainsKey(n)) pByName[n] = code;
                _unitByProd[code] = Int(r["unitcode"]); _typeByProd[code] = Str(r["type"]);
            }
            foreach (DataRow r in GetSrc("SELECT ncode, vname FROM mstitems WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            { string n = Cap(Str(r["vname"]), 150); int c; if (n.Length > 0 && pByName.TryGetValue(n, out c) && !_prodByItem.ContainsKey(Int(r["ncode"]))) _prodByItem[Int(r["ncode"])] = c; }
            _batchByProd = DefaultBatchByProduct();

            // PO line -> migrated pur_order (branch + purorderno)
            Dictionary<string, int> poByNo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, branchcode, purorderno FROM pur_order WHERE type='PO' AND purorderno IS NOT NULL").Tables[0].Rows)
            { string k = Int(r["branchcode"]) + "|" + Str(r["purorderno"]); if (!poByNo.ContainsKey(k)) poByNo[k] = Int(r["code"]); }
            Dictionary<string, string> poNoByHdr = new Dictionary<string, string>();
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhordpl WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
                poNoByHdr[Int(r["nofficeid"]) + "|" + Int(r["ncode"])] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, nordpl FROM trdordpl1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                int office = Int(r["nofficeid"]); string pno; int br, po;
                if (!poNoByHdr.TryGetValue(office + "|" + Int(r["nordpl"]), out pno)) continue;
                _poNoByLine[office + "|" + Int(r["ncode"])] = pno;
                if (_branchByOffice.TryGetValue(office, out br) && poByNo.TryGetValue(br + "|" + pno, out po)) _poByLine[office + "|" + Int(r["ncode"])] = po;
            }
            Console.WriteLine("Maps: branches=" + _branchByOffice.Count + ", parties=" + _custByParty.Count + ", products=" + _prodByItem.Count +
                              ", defaultBatch=" + _batchByProd.Count + ", PO lines linked=" + _poByLine.Count);
        }

        // GRN "Type" dropdown = miscellaneous(GRN/grntype), stockinout.grntype stores the name -> seed the full Stock-In type group.
        static void SeedGrnTypes(SqlConnection tgt)
        {
            HashSet<string> have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT name FROM miscellaneous WHERE module='GRN' AND type='grntype'").Tables[0].Rows) have.Add(Str(r["name"]));
            int added = 0;
            foreach (DataRow r in GetSrc("SELECT DISTINCT vdisplayvalue AS v FROM mstfixedselection WITH (NOLOCK) WHERE vfieldname='nstkintype'").Tables[0].Rows)
            {
                string nm = Cap(Str(r["v"]), 100); if (nm.Length == 0 || have.Contains(nm)) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) VALUES ('GRN','grntype', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); ins.ExecuteNonQuery(); }
                have.Add(nm); added++;
            }
            Console.WriteLine("  miscellaneous(GRN/grntype) seeded: " + added);
        }

        static void LoadSource()
        {
            foreach (DataRow r in GetSrc("SELECT nofficeid, nstkin, nitem, nquantityaccept, nquantityreject, nquantitymissing, nrate, nordpl1 FROM trdstkin1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, nstkin, ncode").Tables[0].Rows)
            {
                if (Int(r["nitem"]) <= 0) { _skipLines++; continue; }
                Add(_inLines, Int(r["nofficeid"]) + "|" + Int(r["nstkin"]), new object[] { Int(r["nitem"]), Dec(r["nquantityaccept"]), Dec(r["nquantityreject"]), Dec(r["nquantitymissing"]), Dec(r["nrate"]), Int(r["nordpl1"]) });
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, nstkou, nitem, nquantity, ndefquantity, nrate, nordrc1 FROM trdstkou1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, nstkou, ncode").Tables[0].Rows)
            {
                if (Int(r["nitem"]) <= 0) { _skipLines++; continue; }
                Add(_outLines, Int(r["nofficeid"]) + "|" + Int(r["nstkou"]), new object[] { Int(r["nitem"]), Dec(r["nquantity"]), Dec(r["ndefquantity"]), Dec(r["nrate"]), Int(r["nordrc1"]) });
            }
            LoadCharges("trdstkin2posttaxchgs", "nstkin", _inCharges);
            LoadCharges("trdstkou2posttaxchgs", "nstkou", _outCharges);
            foreach (DataRow r in GetSrc("SELECT nofficeid, nstkin, vserialno, vbatchno, dexpirydate FROM trdstkin5serialno WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string s = Str(r["vserialno"]);
                if (Str(r["vbatchno"]).Length > 0) s += " (Batch " + Str(r["vbatchno"]) + ")";
                if (r["dexpirydate"] != DBNull.Value) s += " (Exp " + Convert.ToDateTime(r["dexpirydate"]).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + ")";
                List<string> l; string k = Int(r["nofficeid"]) + "|" + Int(r["nstkin"]);
                if (!_serials.TryGetValue(k, out l)) _serials[k] = l = new List<string>();
                if (s.Trim().Length > 0) l.Add(s.Trim());
            }
            Console.WriteLine("  Source: Stock-IN docs with lines " + _inLines.Count + ", Stock-OUT docs with lines " + _outLines.Count + ", serial rows " + _serials.Count);
        }

        static void LoadCharges(string table, string fk, Dictionary<string, List<string>> map)
        {
            foreach (DataRow r in GetSrc("SELECT c.nofficeid, c." + fk + " AS doc, p.vname, c.venteredinamtorper, c.ntaxpercentage, c.namount FROM " + table + " c WITH (NOLOCK) JOIN mstprepostchgs p WITH (NOLOCK) ON p.ncode = c.nmstposttaxchgs WHERE c.nofficeid IN (" + OfficeIn + ") ORDER BY c.nofficeid, c." + fk + ", c.ncode").Tables[0].Rows)
            {
                string v = Str(r["venteredinamtorper"]).ToUpperInvariant() == "PERCENTAGE" ? Dec(r["ntaxpercentage"]).ToString("0.##", CultureInfo.InvariantCulture) + "%" : Dec(r["namount"]).ToString("0.##", CultureInfo.InvariantCulture);
                if (Dec(r["namount"]) == 0 && Dec(r["ntaxpercentage"]) == 0) continue;
                List<string> l; string k = Int(r["nofficeid"]) + "|" + Int(r["doc"]);
                if (!map.TryGetValue(k, out l)) map[k] = l = new List<string>();
                l.Add(Str(r["vname"]) + " " + v);
            }
        }

        static void Add(Dictionary<string, List<object[]>> map, string key, object[] row)
        {
            List<object[]> l; if (!map.TryGetValue(key, out l)) map[key] = l = new List<object[]>(); l.Add(row);
        }
    }

    // ==================================================================
    //  AMC QUOTATION : trhoffer / trdoffer1items / trdoffer2itemsdet -> inqcs (cors='CQ') / inqcsdet
    //  EdifyBiz has no AMC quotation screen; the standard AMC Contract links a Customer Quotation (amc.module='CQ').
    //  One quotation line per equipment serial; Quote Type "AMC - <offer type>", modulename 'AMC Contract';
    //  GST/charges -> prodtax + inq_adjust (as 04/05); status from the serial rows; check list -> taskchecklist 'CQ'.
    //  Mapping: Maping\09_amcquotation_field_mapping.md. - Aftab Alam
    // ==================================================================
    static class AmcQuotation
    {
        static Dictionary<int, string> _fixed, _officeName, _payschedule, _checkName, _taxsetName, _termset, _checkset;
        static readonly Dictionary<string, int> _custByParty = new Dictionary<string, int>();
        static readonly Dictionary<int, int> _branchByContact = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _prodByItem = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _branchByOffice = new Dictionary<int, int>();
        static readonly Dictionary<string, string> _persons = new Dictionary<string, string>();
        static readonly Dictionary<string, List<object[]>> _itemsByKey = new Dictionary<string, List<object[]>>();     // office|offer -> { line ncode, item, qty, offeris, prevnumber }
        static readonly Dictionary<string, List<object[]>> _serialsByLine = new Dictionary<string, List<object[]>>();  // office|offer1 -> serial rows
        static readonly Dictionary<string, string> _contrNo = new Dictionary<string, string>();   // office|trhcontr.ncode -> MC..
        static readonly Dictionary<string, string> _salesNo = new Dictionary<string, string>();   // office|trhsales.ncode -> SA..
        static readonly Dictionary<string, List<object[]>> _checksByKey = new Dictionary<string, List<object[]>>();
        static Dictionary<string, List<object[]>> _chargesByKey;
        static readonly Dictionary<int, int> _mapQuoteType = new Dictionary<int, int>();
        static readonly Dictionary<string, int> _statusByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        static int _inrCode;
        static int _rows, _lines, _skipLines, _errors, _adj, _checks, _docGst, _lineDisc;
        static readonly Dictionary<string, int> _statusCount = new Dictionary<string, int>();

        const string StSigned = "AMC - Contract Signed", StPartly = "AMC - Partly Contract Signed", StClosed = "AMC - Closed", StOpen = "AMC - Open";

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: AMC Quotation (inqcs cors='CQ')");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            _fixed = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
            _payschedule = SrcLookupInt("SELECT ncode, vname FROM mstpaymentschedule WITH (NOLOCK)");
            _termset = SrcLookupInt("SELECT ncode, vname FROM msttermset WITH (NOLOCK)");
            _checkset = SrcLookupInt("SELECT ncode, vname FROM mstcheckset WITH (NOLOCK)");
            _checkName = SrcLookupInt("SELECT ncode, vname FROM mstchecks WITH (NOLOCK)");
            _taxsetName = SrcLookupInt("SELECT ncode, vname FROM msttaxset WITH (NOLOCK)");

            using (SqlConnection tgt = OpenTgt())
            {
                BuildMaps(tgt);
                EnsureUserByCode();
                SeedQuoteTypes(tgt);
                EnsureStatuses(tgt);
                LoadSource();
                _chargesByKey = LoadPostTaxCharges("trdoffer3posttaxchgs", "noffer");
                Console.WriteLine("Maps: customers=" + _custByParty.Count + ", products=" + _prodByItem.Count + ", persons=" + _persons.Count +
                                  ", quoteTypes=" + _mapQuoteType.Count + ", INR=" + _inrCode + ", quotations with lines=" + _itemsByKey.Count);
                Console.WriteLine("-----------------------------------------------------");

                DataTable hdr = GetSrc(@"SELECT ncode, vtrnprefix, ntrnno, dtrndate, nparty, npartycontact, noffertype, nconversion, dfollowupdate, nsalesman,
                                               vrefno, drefdate, vremarks, vcomment, vtermsconditions, npaymentschedule, vbegorend, nterms, ncheckset, nofficeid, addedon, editedon
                                        FROM trhoffer WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY ncode").Tables[0];
                foreach (DataRow h in hdr.Rows)
                {
                    try { InsertRow(tgt, h); }
                    catch (Exception ex) { _errors++; if (_errors <= 20) Console.WriteLine("  [ERROR] AMC Quotation " + Int(h["nofficeid"]) + "|" + Int(h["ncode"]) + ": " + ex.Message); }
                }

                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("  AMC Quotations migrated : " + _rows);
                Console.WriteLine("  inqcsdet lines (serials): " + _lines + "  (skipped, item not migrated: " + _skipLines + ")");
                Console.WriteLine("  Status                  : " + string.Join(", ", _statusCount.Select(kv => kv.Key + " " + kv.Value)));
                Console.WriteLine("  Tax/charges             : quotations with GST % " + _docGst + ", inq_adjust rows " + _adj + ", lines with discount " + _lineDisc);
                Console.WriteLine("  Check list items        : " + _checks + "  (taskchecklist, module='CQ')");
                Console.WriteLine("  Errors                  : " + _errors);
                Console.WriteLine("-----------------------------------------------------");
            }
        }

        static void InsertRow(SqlConnection tgt, DataRow h)
        {
            int office = Int(h["nofficeid"]); string key = office + "|" + Int(h["ncode"]);
            string partyKey = NKey(h["nparty"]);
            object cscode = DBNull.Value, csbranch = DBNull.Value; int cid, br;
            if (partyKey.Length > 0 && _custByParty.TryGetValue(partyKey, out cid)) { cscode = cid; if (_branchByContact.TryGetValue(cid, out br)) csbranch = br; }
            string cperson = ""; if (Int(h["npartycontact"]) > 0 && partyKey.Length > 0) _persons.TryGetValue(partyKey + "|" + Int(h["npartycontact"]), out cperson);
            int ex; object executive = UserByCode.TryGetValue(NKey(h["nsalesman"]), out ex) ? (object)ex : MigrationUser;
            int qt; object quotetype = _mapQuoteType.TryGetValue(Int(h["noffertype"]), out qt) ? (object)qt : DBNull.Value;
            int b; object branch = _branchByOffice.TryGetValue(office, out b) ? (object)b : DBNull.Value;
            object createdon = P(h["addedon"]);
            object updatedon = h["editedon"] != DBNull.Value ? h["editedon"] : createdon;

            // ---- lines: one per serial ----
            List<object[]> items; _itemsByKey.TryGetValue(key, out items);
            decimal itemsAll = 0m, lineTax = 0m; int serials = 0, converted = 0, closed = 0;
            if (items != null)
                foreach (object[] it in items)
                {
                    List<object[]> srs; if (!_serialsByLine.TryGetValue(office + "|" + (int)it[0], out srs)) continue;
                    foreach (object[] s in srs) { itemsAll += (decimal)s[5]; lineTax += (decimal)s[7]; serials++; if ((int)s[10] > 0) converted++; if ((bool)s[11]) closed++; }
                }
            string status = serials == 0 ? StOpen : converted == serials ? StSigned : converted > 0 ? StPartly : closed > 0 ? StClosed : StOpen;
            List<object[]> chg; _chargesByKey.TryGetValue(key, out chg);
            decimal gstRate;
            List<object[]> adj = ComputeCharges(itemsAll, chg, out gstRate);

            List<string> rem = new List<string>();
            string r1 = Str(h["vremarks"]); if (r1.Length > 0) rem.Add(r1);
            string cm = Str(h["vcomment"]); if (cm.Length > 0) rem.Add("Comment: " + cm);
            string ps = LookInt(_payschedule, Int(h["npaymentschedule"])); string be = BeginOrEnd(Str(h["vbegorend"]));   // form "B / E of Period": B = BEGINNING OF PERIOD, E = END OF PERIOD
            if (ps.Length > 0) rem.Add("Payment Schedule: " + ps + (be.Length > 0 ? " - " + be : ""));
            string tset = LookInt(_termset, Int(h["nterms"])); if (tset.Length > 0) rem.Add("Terms & Cond.: " + tset);
            string cset = LookInt(_checkset, Int(h["ncheckset"])); if (cset.Length > 0) rem.Add("Check List: " + cset);
            string conv = LookInt(_fixed, Int(h["nconversion"])); if (conv.Length > 0) rem.Add("Conversion chance: " + conv);

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                try
                {
                    int code;
                    using (SqlCommand c = new SqlCommand(@"INSERT INTO inqcs
                            (cors, inqref, inqdate, followupdate, cscode, csbranch, cperson, executive, status, quotetype, currency,
                             enqrefno, enqrefdate, terms, modulename, approvalstatus, comcode, branchcode, remark, createdby, updatedby, createdon, updatedon)
                        OUTPUT INSERTED.code
                        VALUES ('CQ', @ref, @dt, @fu, @cs, @csb, @cp, @ex, @st, @qt, @cur, @rno, @rdt, @terms, 'AMC Contract', 'Approved', @com, @br, @rem, @cb, @cb, @con, @uon)", tgt, tx))
                    {
                        c.Parameters.AddWithValue("@ref", PS((Str(h["vtrnprefix"]) + Str(h["ntrnno"])).Trim()));
                        c.Parameters.AddWithValue("@dt", P(h["dtrndate"]));
                        c.Parameters.AddWithValue("@fu", P(h["dfollowupdate"]));
                        c.Parameters.AddWithValue("@cs", cscode);
                        c.Parameters.AddWithValue("@csb", csbranch);
                        c.Parameters.AddWithValue("@cp", PS(cperson));
                        c.Parameters.AddWithValue("@ex", executive);
                        int sc; c.Parameters.AddWithValue("@st", _statusByName.TryGetValue(status, out sc) ? (object)sc : DBNull.Value);
                        c.Parameters.AddWithValue("@qt", quotetype);
                        c.Parameters.AddWithValue("@cur", _inrCode > 0 ? (object)_inrCode : DBNull.Value);
                        c.Parameters.AddWithValue("@rno", PS(Cap(Str(h["vrefno"]), 200)));
                        c.Parameters.AddWithValue("@rdt", P(h["drefdate"]));
                        c.Parameters.AddWithValue("@terms", PS(Str(h["vtermsconditions"])));
                        c.Parameters.AddWithValue("@com", CompanyCode);
                        c.Parameters.AddWithValue("@br", branch);
                        c.Parameters.AddWithValue("@rem", PS(string.Join(" | ", rem)));
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        c.Parameters.AddWithValue("@con", createdon);
                        c.Parameters.AddWithValue("@uon", updatedon);
                        code = Convert.ToInt32(c.ExecuteScalar());
                    }

                    decimal migratedValue = 0m; int lineN = 0, discN = 0;
                    if (items != null)
                        foreach (object[] it in items)
                        {
                            int pcode;
                            List<object[]> srs; _serialsByLine.TryGetValue(office + "|" + (int)it[0], out srs);
                            if (!_prodByItem.TryGetValue((int)it[1], out pcode)) { _skipLines += srs != null ? srs.Count : 1; continue; }
                            string offeris = LookInt(_fixed, (int)it[3]);
                            string prev = PrevDoc(office, offeris, (int)it[4]);
                            if (srs == null || srs.Count == 0)
                            {
                                InsertLine(tgt, tx, code, pcode, (decimal)it[2], DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value,
                                           string.Join("<br/>", new[] { offeris, prev }.Where(x => x.Length > 0)), createdon, updatedon);
                                lineN++; continue;
                            }
                            foreach (object[] s in srs)
                            {
                                decimal master = (decimal)s[3], dpct = (decimal)s[4], rate = (decimal)s[5];
                                object price, disc = DBNull.Value, dp = DBNull.Value;
                                if (dpct != 0 && master > rate) { price = master; disc = master - rate; dp = dpct; discN++; }
                                else price = rate == 0 ? (object)DBNull.Value : rate;
                                decimal lr = gstRate; string tsn;
                                if (lr <= 0 && (int)s[6] > 0 && (decimal)s[7] != 0 && _taxsetName.TryGetValue((int)s[6], out tsn)) lr = GstRateIn(tsn);
                                string pdesc = SerialText(office, s, offeris, prev);
                                InsertLine(tgt, tx, code, pcode, 1m, price, disc, dp, lr > 0 ? (object)lr : DBNull.Value, PS(Cap((string)s[0], 50)), PS(Cap((string)s[1], 255)), pdesc, createdon, updatedon);
                                migratedValue += rate; lineN++;
                            }
                        }

                    decimal otherItems = Math.Round(itemsAll - migratedValue, 2);
                    if (otherItems != 0) adj.Insert(0, new object[] { "Other items (eBizWiz)", 0m, otherItems });
                    if (lineTax != 0) adj.Add(new object[] { "Tax on items (eBizWiz)", 0m, lineTax });
                    int adjN = InsertInqAdjustments(tgt, tx, code, adj, createdon, updatedon);

                    int clN = 0;
                    List<object[]> cl;
                    if (_checksByKey.TryGetValue(key, out cl))
                    {
                        int sort = 0;
                        foreach (object[] ck in cl)
                        {
                            bool done = (bool)ck[2];
                            string title = LookInt(_checkName, (int)ck[0]); if (title.Length == 0) title = "Check " + (int)ck[0];
                            object duedate = ck[1] != DBNull.Value ? ck[1] : (done ? (ck[3] != DBNull.Value ? ck[3] : (ck[6] != DBNull.Value ? ck[6] : (object)DateTime.Today)) : DateTime.Today.AddDays(7));
                            int au; object assignto = UserByCode.TryGetValue((string)ck[4], out au) ? (object)au : DBNull.Value;
                            object clCreated = ck[6] != DBNull.Value ? ck[6] : createdon;
                            object clUpdated = done ? (ck[3] != DBNull.Value ? ck[3] : (ck[7] != DBNull.Value ? ck[7] : clCreated)) : (ck[7] != DBNull.Value ? ck[7] : clCreated);
                            // Task checklist module='CQ', modulecode = quotation inqcs.code (inquiry.js shows the tab for SO and CQ with aInquiry[16] as tag)
                            using (SqlCommand q = new SqlCommand(@"INSERT INTO taskchecklist (title, status, module, modulecode, duedate, assignto, remark, sort, createdby, updatedby, createdon, updatedon)
                                                                 VALUES (@t, @s, 'CQ', @mc, @dd, @at, @rm, @sr, @cb, @cb, @con, @uon)", tgt, tx))
                            {
                                q.Parameters.AddWithValue("@t", PS(title));
                                q.Parameters.AddWithValue("@s", done ? 1 : 0);
                                q.Parameters.AddWithValue("@mc", code);
                                q.Parameters.AddWithValue("@dd", duedate);
                                q.Parameters.AddWithValue("@at", assignto);
                                q.Parameters.AddWithValue("@rm", PS((string)ck[5]));
                                q.Parameters.AddWithValue("@sr", sort++);
                                q.Parameters.AddWithValue("@cb", MigrationUser);
                                q.Parameters.AddWithValue("@con", clCreated);
                                q.Parameters.AddWithValue("@uon", clUpdated);
                                q.ExecuteNonQuery();
                            }
                            clN++;
                        }
                    }

                    tx.Commit();
                    _rows++; _lines += lineN; _adj += adjN; _checks += clN; _lineDisc += discN; if (gstRate > 0) _docGst++;
                    int cnt; _statusCount.TryGetValue(status, out cnt); _statusCount[status] = cnt + 1;
                    if (_rows % 1000 == 0) Console.WriteLine("  ... " + _rows + " AMC Quotations migrated");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        static void InsertLine(SqlConnection tgt, SqlTransaction tx, int inqcode, int pcode, decimal qty, object price, object disc, object dpct, object tax, object srno, object location, string pdesc, object con, object uon)
        {
            using (SqlCommand d = new SqlCommand(@"INSERT INTO inqcsdet (inqcode, pcode, quantity, price, discount, discountpercent, prodtax, prodsrno, location, pdesc, currency, createdby, updatedby, createdon, updatedon)
                                                 VALUES (@i, @p, @q, @pr, @disc, @dp, @tax, @sr, @loc, @pd, @cur, @cb, @cb, @con, @uon)", tgt, tx))
            {
                d.Parameters.AddWithValue("@i", inqcode);
                d.Parameters.AddWithValue("@p", pcode);
                d.Parameters.AddWithValue("@q", qty);
                d.Parameters.AddWithValue("@pr", price);
                d.Parameters.AddWithValue("@disc", disc);
                d.Parameters.AddWithValue("@dp", dpct);
                d.Parameters.AddWithValue("@tax", tax);
                d.Parameters.AddWithValue("@sr", srno);
                d.Parameters.AddWithValue("@loc", location);
                d.Parameters.AddWithValue("@pd", PS(pdesc));
                d.Parameters.AddWithValue("@cur", _inrCode > 0 ? (object)_inrCode : DBNull.Value);
                d.Parameters.AddWithValue("@cb", MigrationUser);
                d.Parameters.AddWithValue("@con", con);
                d.Parameters.AddWithValue("@uon", uon);
                d.ExecuteNonQuery();
            }
        }

        // Product Description (CKEditor HTML, shown under the product name in the saksham quotation view)
        static string SerialText(int office, object[] s, string offeris, string prev)
        {
            List<string> p = new List<string>();
            if (((string)s[0]).Length > 0) p.Add("S/N: " + (string)s[0]);
            if (((string)s[1]).Length > 0) p.Add("Location: " + (string)s[1]);
            string ct = LookInt(_fixed, (int)s[2]); if (ct.Length > 0) p.Add("Contract Type: " + ct);
            string period = Dt(s[8]) + (s[9] != DBNull.Value ? " - " + Dt(s[9]) : "") + ((int)s[12] > 0 ? " (" + (int)s[12] + " months)" : "");
            if (period.Trim().Length > 0) p.Add("Period: " + period.Trim());
            if ((int)s[13] > 0) p.Add("PM Visits: " + (int)s[13]);
            if (s[14] != DBNull.Value) p.Add("Old End Date: " + Dt(s[14]));
            if (offeris.Length > 0) p.Add(offeris);
            if (prev.Length > 0) p.Add(prev);
            string cn; if ((int)s[10] > 0) p.Add("Contract: " + (_contrNo.TryGetValue(office + "|" + (int)s[10], out cn) ? cn : "#" + (int)s[10]));
            else if ((bool)s[11]) p.Add("Closed");
            return string.Join("<br/>", p);
        }

        static string PrevDoc(int office, string offeris, int prev)
        {
            if (prev <= 0) return "";
            string no;
            if (offeris.IndexOf("RENEWAL", StringComparison.OrdinalIgnoreCase) >= 0) return "Previous Contract: " + (_contrNo.TryGetValue(office + "|" + prev, out no) ? no : "#" + prev);
            if (offeris.IndexOf("SIGNUP", StringComparison.OrdinalIgnoreCase) >= 0) return "Sales Invoice: " + (_salesNo.TryGetValue(office + "|" + prev, out no) ? no : "#" + prev);
            return "";
        }

        static string Dt(object v) { return v == DBNull.Value || v == null ? "" : Convert.ToDateTime(v).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture); }

        static void LoadSource()
        {
            foreach (DataRow r in GetSrc("SELECT nofficeid, noffer, ncode, nitem, nquantity, nofferis, nprevnumber FROM trdoffer1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, noffer, ncode").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["noffer"]);
                List<object[]> l; if (!_itemsByKey.TryGetValue(k, out l)) _itemsByKey[k] = l = new List<object[]>();
                l.Add(new object[] { Int(r["ncode"]), Int(r["nitem"]), Dec(r["nquantity"]), Int(r["nofferis"]), Int(r["nprevnumber"]) });
            }
            // { 0 serial, 1 location, 2 contrtype, 3 masterrate, 4 discpct, 5 rate, 6 taxset, 7 taxamt, 8 start, 9 end, 10 newcontract, 11 closed, 12 months, 13 pmvisits, 14 old end }
            foreach (DataRow r in GetSrc(@"SELECT nofficeid, noffer1, vserialno, vlocation, ncontrtype, nmasterrate, ndiscountperc, nrate, ntaxset, ntaxamt, dstartdate, denddate,
                                                  nnewcontractno, bclosed, nmonths, npmvisits, doldenddate
                                           FROM trdoffer2itemsdet WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, noffer1, ncode").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["noffer1"]);
                List<object[]> l; if (!_serialsByLine.TryGetValue(k, out l)) _serialsByLine[k] = l = new List<object[]>();
                l.Add(new object[] { Str(r["vserialno"]), Str(r["vlocation"]), Int(r["ncontrtype"]), Dec(r["nmasterrate"]), Dec(r["ndiscountperc"]), Dec(r["nrate"]),
                                     Int(r["ntaxset"]), Dec(r["ntaxamt"]), P(r["dstartdate"]), P(r["denddate"]), Int(r["nnewcontractno"]), Bool(r["bclosed"]),
                                     Int(r["nmonths"]), Int(r["npmvisits"]), P(r["doldenddate"]) });
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhcontr WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
                _contrNo[Int(r["nofficeid"]) + "|" + Int(r["ncode"])] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhsales WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
                _salesNo[Int(r["nofficeid"]) + "|" + Int(r["ncode"])] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            int n = 0;
            foreach (DataRow r in GetSrc("SELECT nofficeid, noffer, ncheck, dexpdate, bdone, ddonedate, nallotedto, vremarks, addedon, editedon FROM trdoffer6checks WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY noffer, ncode").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["noffer"]);
                List<object[]> l; if (!_checksByKey.TryGetValue(k, out l)) _checksByKey[k] = l = new List<object[]>();
                l.Add(new object[] { Int(r["ncheck"]), P(r["dexpdate"]), Bool(r["bdone"]), P(r["ddonedate"]), NKey(r["nallotedto"]), Str(r["vremarks"]), P(r["addedon"]), P(r["editedon"]) });
                n++;
            }
            Console.WriteLine("  Source: product lines for " + _itemsByKey.Count + " quotations, serial groups " + _serialsByLine.Count + ", check items " + n);
        }

        // Quote Type master (miscellaneous Inquiry/Quote Type, already used by saksham CQ): full noffertype group as "AMC - <name>".
        static void SeedQuoteTypes(SqlConnection tgt)
        {
            Dictionary<string, int> have = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM miscellaneous WHERE module='Inquiry' AND type='Quote Type'").Tables[0].Rows) { string nm = Str(r["name"]); if (!have.ContainsKey(nm)) have[nm] = Int(r["code"]); }
            int added = 0;
            foreach (DataRow r in GetSrc("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK) WHERE vfieldname='noffertype'").Tables[0].Rows)
            {
                string nm = "AMC - " + Str(r["vdisplayvalue"]).Trim(); int c;
                if (!have.TryGetValue(nm, out c))
                {
                    using (SqlCommand ins = new SqlCommand("INSERT INTO miscellaneous (module, type, name, active, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES ('Inquiry','Quote Type', @n, 1, 0, @cb, GETDATE(), @cb, GETDATE())", tgt))
                    { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); c = Convert.ToInt32(ins.ExecuteScalar()); }
                    have[nm] = c; added++;
                }
                _mapQuoteType[Int(r["ncode"])] = c;
            }
            Console.WriteLine("  miscellaneous(Inquiry/Quote Type) AMC values added: " + added);
        }

        // Quotation status master (status module='Quotation', read by the CQ Status dropdown): outcome values from the serial rows.
        static void EnsureStatuses(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, name FROM status WHERE module='Quotation'").Tables[0].Rows) { string nm = Str(r["name"]); if (!_statusByName.ContainsKey(nm)) _statusByName[nm] = Int(r["code"]); }
            int added = 0;
            foreach (string nm in new[] { StSigned, StPartly, StClosed, StOpen })
            {
                if (_statusByName.ContainsKey(nm)) continue;
                using (SqlCommand ins = new SqlCommand("INSERT INTO status (name, module, sort, makedefault, createdby, updatedby, createdon, updatedon) OUTPUT INSERTED.code VALUES (@n, 'Quotation', 0, 0, @cb, @cb, GETDATE(), GETDATE())", tgt))
                { ins.Parameters.AddWithValue("@n", nm); ins.Parameters.AddWithValue("@cb", MigrationUser); _statusByName[nm] = Convert.ToInt32(ins.ExecuteScalar()); }
                added++;
            }
            Console.WriteLine("  status(Quotation) AMC values added: " + added);
        }

        static void BuildMaps(SqlConnection tgt)
        {
            foreach (DataRow r in GetTgt("SELECT code, name, sname FROM currency").Tables[0].Rows)
            {
                string nm = Str(r["name"]), sn = Str(r["sname"]);
                if (_inrCode == 0 && (sn.Equals("INR", StringComparison.OrdinalIgnoreCase) || nm.IndexOf("Indian Rupee", StringComparison.OrdinalIgnoreCase) >= 0)) _inrCode = Int(r["code"]);
            }
            foreach (int office in Offices)
            {
                string nm; string place = _officeName.TryGetValue(office, out nm) && nm.Length > 0 ? Cap(nm, 100) : "Office " + office;
                using (SqlCommand q = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                { q.Parameters.AddWithValue("@cc", CompanyCode); q.Parameters.AddWithValue("@pl", place); object o = q.ExecuteScalar(); if (o != null && o != DBNull.Value) _branchByOffice[office] = Convert.ToInt32(o); }
            }
            Dictionary<string, int> cByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM contact WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !cByName.ContainsKey(n)) cByName[n] = Int(r["code"]); }
            foreach (DataRow r in GetSrc("SELECT ncode, ntitle, vname FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string k = NKey(r["ncode"]); if (k.Length == 0) continue;
                string nm = Cap(Join(LookInt(_fixed, Int(r["ntitle"])), Str(r["vname"])), 150);
                int c; if (nm.Length > 0 && cByName.TryGetValue(nm, out c) && !_custByParty.ContainsKey(k)) _custByParty[k] = c;
            }
            foreach (KeyValuePair<string, int> kv in PartyToContact) _custByParty[kv.Key] = kv.Value;
            foreach (DataRow r in GetTgt("SELECT code, ccode, isnull(isdefault,0) as isdefault FROM mltaddress WITH (NOLOCK) ORDER BY ccode, isdefault DESC, code").Tables[0].Rows)
            { int cc = Int(r["ccode"]); if (cc > 0 && !_branchByContact.ContainsKey(cc)) _branchByContact[cc] = Int(r["code"]); }
            Dictionary<string, int> pByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM product WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !pByName.ContainsKey(n)) pByName[n] = Int(r["code"]); }
            foreach (DataRow r in GetSrc("SELECT ncode, vname FROM mstitems WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            { string n = Cap(Str(r["vname"]), 150); int c; if (n.Length > 0 && pByName.TryGetValue(n, out c) && !_prodByItem.ContainsKey(Int(r["ncode"]))) _prodByItem[Int(r["ncode"])] = c; }

            // contact persons: same resolution as Customer Quotation (party|person -> mltcontact code)
            Dictionary<string, int> mltMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, ccode, name FROM mltcontact WITH (NOLOCK)").Tables[0].Rows)
            { int code = Int(r["code"]), cc = Int(r["ccode"]); string name = Str(r["name"]); if (code > 0 && cc > 0 && name.Length > 0) { string k = cc + "|" + name; if (!mltMap.ContainsKey(k)) mltMap[k] = code; } }
            foreach (DataRow dr in GetSrc("SELECT nparty, ncode, ntitle, vcontactperson FROM msdparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND LTRIM(RTRIM(ISNULL(vcontactperson,''))) <> ''").Tables[0].Rows)
            {
                string partyKey = NKey(dr["nparty"]); int personNo = Int(dr["ncode"]); if (partyKey.Length == 0 || personNo <= 0) continue;
                string key = partyKey + "|" + personNo; if (_persons.ContainsKey(key)) continue;
                string raw = Str(dr["vcontactperson"]); string formatted = Cap(Join(LookInt(_fixed, Int(dr["ntitle"])), raw), 150);
                string val = ""; int cc2, m1, m2;
                if (_custByParty.TryGetValue(partyKey, out cc2))
                {
                    if (mltMap.TryGetValue(cc2 + "|" + formatted, out m1)) val = m1.ToString();
                    else if (mltMap.TryGetValue(cc2 + "|" + raw, out m2)) val = m2.ToString();
                }
                _persons[key] = val;
            }
        }

        static string Join(string a, string b) { return string.Join(" ", new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(); }
    }

    // ==================================================================
    //  AMC CONTRACT : trhcontr / trdcontr1items / trdcontr2itemsdet / trdcontr3bills / trdcontr7pmvisit
    //  -> amc / contractdetails (one per serial) / billingcycle / contractcall type 'PMS'.
    //  Quotation link amc.module='CQ' + modulecode (migrated AMC Quotation), renewal oldcontractcode + renew=1,
    //  amc.salcode never set (edit would delete that invoice), GST/charges in remarks (no tax on the standard contract).
    //  Mapping: Maping\11_amccontract_field_mapping.md. Run AFTER AMC Quotation. - Aftab Alam
    // ==================================================================
    static class AmcContract
    {
        static Dictionary<int, string> _fixed, _officeName, _payschedule, _termset, _checkset;
        static readonly Dictionary<string, int> _custByParty = new Dictionary<string, int>();
        static readonly Dictionary<int, int> _branchByContact = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _prodByItem = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _branchByOffice = new Dictionary<int, int>();
        static readonly Dictionary<string, int> _persons = new Dictionary<string, int>();                 // party|person -> mltcontact.code
        static readonly Dictionary<string, List<object[]>> _itemsByKey = new Dictionary<string, List<object[]>>();    // office|contr -> { line ncode, item, contractis, previousno }
        static readonly Dictionary<string, List<object[]>> _serialsByLine = new Dictionary<string, List<object[]>>(); // office|contr1 -> serial rows
        static readonly Dictionary<string, List<object[]>> _billsByKey = new Dictionary<string, List<object[]>>();
        static readonly Dictionary<string, List<object[]>> _visitsBySerial = new Dictionary<string, List<object[]>>(); // office|contr2 -> { sched, actual, calls }
        static readonly Dictionary<string, string> _quoteRef = new Dictionary<string, string>();   // office|trhoffer.ncode -> QA..
        static readonly Dictionary<string, int> _cqByKey = new Dictionary<string, int>();          // branch|QA.. -> inqcs.code
        static readonly Dictionary<string, string> _salesNo = new Dictionary<string, string>();    // office|trhsales.ncode -> SA..
        static readonly Dictionary<string, object> _salesDt = new Dictionary<string, object>();
        static readonly Dictionary<string, string> _contrNo = new Dictionary<string, string>();    // office|trhcontr.ncode -> MC..
        static readonly Dictionary<string, string> _callNo = new Dictionary<string, string>();     // office|trhcalls.ncode -> call no
        static readonly Dictionary<string, int> _amcBySrc = new Dictionary<string, int>();         // office|trhcontr.ncode -> amc.code
        static readonly List<int[]> _renewals = new List<int[]>();                                  // { new amc.code, office, previous trhcontr.ncode }
        static Dictionary<string, List<object[]>> _chargesByKey;
        static int _stOpen, _stClosed;
        static int _rows, _lines, _skipLines, _bills, _pms, _pmsClosed, _quoteLinked, _renewLinked, _errors;

        public static void Run()
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("   eBizWiz  ->  EdifyBiz   |   Module: AMC Contract (amc / contractdetails / billingcycle / PMS)");
            Console.WriteLine("   Offices in scope: " + OfficeIn + "  (fresh-DB direct insert)");
            Console.WriteLine("=====================================================");

            _fixed = SrcLookupInt("SELECT ncode, vdisplayvalue FROM mstfixedselection WITH (NOLOCK)");
            _officeName = SrcLookupInt("SELECT ncode, vcompanyname FROM mstoffice WITH (NOLOCK)");
            _payschedule = SrcLookupInt("SELECT ncode, vname FROM mstpaymentschedule WITH (NOLOCK)");
            _termset = SrcLookupInt("SELECT ncode, vname FROM msttermset WITH (NOLOCK)");
            _checkset = SrcLookupInt("SELECT ncode, vname FROM mstcheckset WITH (NOLOCK)");

            using (SqlConnection tgt = OpenTgt())
            {
                BuildMaps(tgt);
                EnsureUserByCode();
                EnsureStatuses(tgt);
                LoadSource();
                _chargesByKey = LoadPostTaxCharges("trdcontr4posttaxchgs", "ncontr");
                Console.WriteLine("Maps: customers=" + _custByParty.Count + ", products=" + _prodByItem.Count + ", AMC quotations=" + _cqByKey.Count +
                                  ", status Open=" + _stOpen + " Closed=" + _stClosed);
                Console.WriteLine("-----------------------------------------------------");

                DataTable hdr = GetSrc(@"SELECT ncode, vtrnprefix, ntrnno, dtrndate, nparty, npartycontact, namcquoteno, nsalesman, vpono, dpodate, vrefno, drefdate,
                                               vremarks, vcomment, nitemtotal, ntotalamount, namountrecd, npaymentschedule, vbegorend, nterms, ncheckset, nofficeid, addedon, editedon
                                        FROM trhcontr WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY ncode").Tables[0];
                foreach (DataRow h in hdr.Rows)
                {
                    try { InsertRow(tgt, h); }
                    catch (Exception ex) { _errors++; if (_errors <= 20) Console.WriteLine("  [ERROR] AMC Contract " + Int(h["nofficeid"]) + "|" + Int(h["ncode"]) + ": " + ex.Message); }
                }
                LinkRenewals(tgt);

                Console.WriteLine("-----------------------------------------------------");
                Console.WriteLine("  AMC Contracts migrated  : " + _rows + "  (quotation linked " + _quoteLinked + ", renewal linked " + _renewLinked + ")");
                Console.WriteLine("  contractdetails (serial): " + _lines + "  (skipped, item not migrated: " + _skipLines + ")");
                Console.WriteLine("  billingcycle            : " + _bills);
                Console.WriteLine("  PMS visits              : " + _pms + "  (closed " + _pmsClosed + ")");
                Console.WriteLine("  Errors                  : " + _errors);
                Console.WriteLine("-----------------------------------------------------");
            }
        }

        static void InsertRow(SqlConnection tgt, DataRow h)
        {
            int office = Int(h["nofficeid"]); int srcCode = Int(h["ncode"]); string key = office + "|" + srcCode;
            string contractno = (Str(h["vtrnprefix"]) + Str(h["ntrnno"])).Trim();
            string partyKey = NKey(h["nparty"]);
            object ccode = DBNull.Value, cbranch = DBNull.Value; int cid, br;
            if (partyKey.Length > 0 && _custByParty.TryGetValue(partyKey, out cid)) { ccode = cid; if (_branchByContact.TryGetValue(cid, out br)) cbranch = br; }
            int mlt; object contactperson = Int(h["npartycontact"]) > 0 && _persons.TryGetValue(partyKey + "|" + Int(h["npartycontact"]), out mlt) ? (object)mlt : DBNull.Value;
            int ex; object executive = UserByCode.TryGetValue(NKey(h["nsalesman"]), out ex) ? (object)ex : MigrationUser;
            int b; object branch = _branchByOffice.TryGetValue(office, out b) ? (object)b : DBNull.Value;
            object createdon = P(h["addedon"]);
            object updatedon = h["editedon"] != DBNull.Value ? h["editedon"] : createdon;

            // serial rows of the contract -> type (most serials), period, dates, item total
            List<object[]> items; _itemsByKey.TryGetValue(key, out items);
            int comp = 0, noncomp = 0; DateTime? start = null, end = null; decimal itemsAll = 0m; int months = 0;
            if (items != null)
                foreach (object[] it in items)
                {
                    List<object[]> srs; if (!_serialsByLine.TryGetValue(key + "|" + (int)it[0], out srs)) continue;
                    foreach (object[] s in srs)
                    {
                        string ct = LookInt(_fixed, (int)s[3]).ToUpperInvariant();
                        if (ct.Contains("NON-COMPREHENSIVE")) noncomp++; else if (ct.Contains("COMPREHENSIVE")) comp++;
                        DateTime? sd = s[6] as DateTime?, ed = s[7] as DateTime?;
                        if (sd.HasValue && (!start.HasValue || sd < start)) start = sd;
                        if (ed.HasValue && (!end.HasValue || ed > end)) end = ed;
                        if (months == 0 && (int)s[8] > 0) months = (int)s[8];
                        itemsAll += (decimal)s[4];
                    }
                }
            object amctype = comp + noncomp == 0 ? (object)DBNull.Value : (comp > noncomp ? "1" : "2");

            object module = DBNull.Value, modulecode = DBNull.Value; string qref; int cq;
            if (Int(h["namcquoteno"]) > 0 && _quoteRef.TryGetValue(office + "|" + Int(h["namcquoteno"]), out qref) && branch != DBNull.Value && _cqByKey.TryGetValue(b + "|" + qref, out cq))
            { module = "CQ"; modulecode = cq; }

            List<string> rem = new List<string>();
            string r1 = Str(h["vremarks"]); if (r1.Length > 0) rem.Add(r1);
            string cm = Str(h["vcomment"]); if (cm.Length > 0) rem.Add("Comment: " + cm);
            string rf = Str(h["vrefno"]); if (rf.Length > 0) rem.Add("Ref No: " + rf);
            if (h["drefdate"] != DBNull.Value) rem.Add("Ref Date: " + Dt(h["drefdate"]));
            if (Int(h["namcquoteno"]) > 0 && module == DBNull.Value && _quoteRef.TryGetValue(office + "|" + Int(h["namcquoteno"]), out qref)) rem.Add("AMC Quotation: " + qref);
            List<object[]> chg; decimal gstRate;
            if (_chargesByKey.TryGetValue(key, out chg))
            {
                List<object[]> rows = ComputeCharges(itemsAll, chg, out gstRate);
                if (rows.Count > 0) rem.Add("Charges (eBizWiz): " + string.Join("; ", rows.Select(a => (string)a[0] + " " + ((decimal)a[2]).ToString("0.##", CultureInfo.InvariantCulture))));
            }
            if (Dec(h["ntotalamount"]) != 0) rem.Add("eBizWiz Contract Total: " + Dec(h["ntotalamount"]).ToString("0.##", CultureInfo.InvariantCulture));
            if (Dec(h["namountrecd"]) != 0) rem.Add("Amount Received: " + Dec(h["namountrecd"]).ToString("0.##", CultureInfo.InvariantCulture));
            string ps = LookInt(_payschedule, Int(h["npaymentschedule"])); string be = BeginOrEnd(Str(h["vbegorend"]));   // form "B / E of Period"
            string tset = LookInt(_termset, Int(h["nterms"])); if (tset.Length > 0) rem.Add("Terms & Cond.: " + tset);
            string cset = LookInt(_checkset, Int(h["ncheckset"])); if (cset.Length > 0) rem.Add("Check List: " + cset);

            using (SqlTransaction tx = tgt.BeginTransaction())
            {
                try
                {
                    int code;
                    using (SqlCommand c = new SqlCommand(@"INSERT INTO amc (ccode, contractno, contractdate, startdate, enddate, amctype, executive, renew, branch, ponum, podate,
                                                                            contractyears, contractmonths, remarks, contactperson, module, modulecode, paymentterms, contactbranch,
                                                                            createdby, createdon, updatedby, updatedon)
                                                         OUTPUT INSERTED.code
                                                         VALUES (@cc, @no, @dt, @sd, @ed, @type, @ex, 0, @br, @po, @podt, @yr, @mo, @rem, @cp, @mod, @mc, @pt, @cbr, @cb, @con, @cb, @uon)", tgt, tx))
                    {
                        c.Parameters.AddWithValue("@cc", ccode);
                        c.Parameters.AddWithValue("@no", PS(contractno));
                        c.Parameters.AddWithValue("@dt", P(h["dtrndate"]));
                        c.Parameters.AddWithValue("@sd", start.HasValue ? (object)start.Value : DBNull.Value);
                        c.Parameters.AddWithValue("@ed", end.HasValue ? (object)end.Value : DBNull.Value);
                        c.Parameters.AddWithValue("@type", amctype);
                        c.Parameters.AddWithValue("@ex", executive);
                        c.Parameters.AddWithValue("@br", branch);
                        c.Parameters.AddWithValue("@po", PS(Cap(Str(h["vpono"]), 250)));
                        c.Parameters.AddWithValue("@podt", P(h["dpodate"]));
                        c.Parameters.AddWithValue("@yr", months / 12);
                        c.Parameters.AddWithValue("@mo", months % 12);
                        c.Parameters.AddWithValue("@rem", PS(string.Join(" | ", rem)));
                        c.Parameters.AddWithValue("@cp", contactperson);
                        c.Parameters.AddWithValue("@mod", module);
                        c.Parameters.AddWithValue("@mc", modulecode);
                        c.Parameters.AddWithValue("@pt", PS(ps.Length > 0 ? ps + (be.Length > 0 ? " - " + be : "") : ""));
                        c.Parameters.AddWithValue("@cbr", cbranch);
                        c.Parameters.AddWithValue("@cb", MigrationUser);
                        c.Parameters.AddWithValue("@con", createdon);
                        c.Parameters.AddWithValue("@uon", updatedon);
                        code = Convert.ToInt32(c.ExecuteScalar());
                    }

                    int lineN = 0, pmsN = 0, pmsClosedN = 0; List<int> prevContracts = new List<int>();
                    if (items != null)
                        foreach (object[] it in items)
                        {
                            string contractis = LookInt(_fixed, (int)it[2]);
                            if ((int)it[3] > 0 && contractis.IndexOf("FROM CONTRACT", StringComparison.OrdinalIgnoreCase) >= 0 && !prevContracts.Contains((int)it[3])) prevContracts.Add((int)it[3]);
                            string prevNo; string prevText = (int)it[3] > 0 && contractis.IndexOf("FROM CONTRACT", StringComparison.OrdinalIgnoreCase) >= 0 ? "Previous Contract: " + (_contrNo.TryGetValue(office + "|" + (int)it[3], out prevNo) ? prevNo : "#" + (int)it[3]) : "";
                            object invNo = DBNull.Value, invDt = DBNull.Value; string sno;
                            if ((int)it[3] > 0 && contractis.IndexOf("FROM WARRANTY", StringComparison.OrdinalIgnoreCase) >= 0 && _salesNo.TryGetValue(office + "|" + (int)it[3], out sno))
                            { invNo = Cap(sno, 50); invDt = _salesDt[office + "|" + (int)it[3]]; }
                            List<object[]> srs; if (!_serialsByLine.TryGetValue(key + "|" + (int)it[0], out srs)) continue;
                            int pcode;
                            if (!_prodByItem.TryGetValue((int)it[1], out pcode)) { _skipLines += srs.Count; continue; }
                            foreach (object[] s in srs)
                            {
                                int months1 = (int)s[8], pm = (int)s[9];
                                object intervals = DBNull.Value;
                                if (months1 > 0 && (pm * 12) % months1 == 0) { int py = pm * 12 / months1; if (py == 0 || py == 1 || py == 2 || py == 3 || py == 4 || py == 6 || py == 12) intervals = py; }
                                List<string> lr = new List<string>();
                                string ct = LookInt(_fixed, (int)s[3]); if (ct.Length > 0) lr.Add("Contract Type: " + ct);
                                if (months1 > 0) lr.Add("Months: " + months1);
                                lr.Add("PM Visits: " + pm);
                                if (contractis.Length > 0) lr.Add(contractis);
                                if (prevText.Length > 0) lr.Add(prevText);
                                if ((decimal)s[5] != 0) lr.Add("Master Rate: " + ((decimal)s[10]).ToString("0.##", CultureInfo.InvariantCulture) + ", Discount %: " + ((decimal)s[5]).ToString("0.##", CultureInfo.InvariantCulture));
                                if ((bool)s[11]) lr.Add("Closed");
                                int det;
                                using (SqlCommand d = new SqlCommand(@"INSERT INTO contractdetails (contractcode, productcode, srno, invoiceno, invoicedate, installationdate, intervals, amount, location, startdate, enddate, iscomponent, remark, quantity, createdby, createdon, updatedby, updatedon)
                                                                     OUTPUT INSERTED.code
                                                                     VALUES (@c, @p, @sr, @inv, @invdt, @inst, @int, @amt, @loc, @sd, @ed, 0, @rem, 1, @cb, @con, @cb, @uon)", tgt, tx))
                                {
                                    d.Parameters.AddWithValue("@c", code);
                                    d.Parameters.AddWithValue("@p", pcode);
                                    d.Parameters.AddWithValue("@sr", PS(Cap((string)s[1], 50)));
                                    d.Parameters.AddWithValue("@inv", invNo);
                                    d.Parameters.AddWithValue("@invdt", invDt);
                                    d.Parameters.AddWithValue("@inst", s[12]);
                                    d.Parameters.AddWithValue("@int", intervals);
                                    d.Parameters.AddWithValue("@amt", (decimal)s[4]);
                                    d.Parameters.AddWithValue("@loc", PS((string)s[2]));
                                    d.Parameters.AddWithValue("@sd", s[6] ?? DBNull.Value);
                                    d.Parameters.AddWithValue("@ed", s[7] ?? DBNull.Value);
                                    d.Parameters.AddWithValue("@rem", PS(string.Join(" | ", lr)));
                                    d.Parameters.AddWithValue("@cb", MigrationUser);
                                    d.Parameters.AddWithValue("@con", createdon);
                                    d.Parameters.AddWithValue("@uon", updatedon);
                                    det = Convert.ToInt32(d.ExecuteScalar());
                                }
                                lineN++;

                                // PM visits of this serial -> contractcall PMS (real schedule / done dates)
                                List<object[]> vs;
                                if (_visitsBySerial.TryGetValue(office + "|" + (int)s[0], out vs))
                                {
                                    int k = 0;
                                    foreach (object[] v in vs)
                                    {
                                        bool done = v[1] != DBNull.Value;
                                        string cn; string callRemark = (int)v[2] > 0 ? "Call: " + (_callNo.TryGetValue(office + "|" + (int)v[2], out cn) ? cn : "#" + (int)v[2]) : null;
                                        using (SqlCommand q = new SqlCommand(@"INSERT INTO contractcall (contractdtcode, date, complaintdate, status, type, pmscomplaintno, ccode, productcode, serialno, closedt, remark, contactperson, createdby, createdon, updatedby, updatedon)
                                                                             VALUES (@dc, @dt, @dt, @st, 'PMS', @no, @cc, @p, @sr, @cl, @rem, @cp, @cb, @con, @cb, @uon)", tgt, tx))
                                        {
                                            q.Parameters.AddWithValue("@dc", det);
                                            q.Parameters.AddWithValue("@dt", v[0]);
                                            q.Parameters.AddWithValue("@st", done ? _stClosed : _stOpen);
                                            q.Parameters.AddWithValue("@no", "PMS" + det.ToString("0000000") + "_" + k);
                                            q.Parameters.AddWithValue("@cc", ccode);
                                            q.Parameters.AddWithValue("@p", pcode);
                                            q.Parameters.AddWithValue("@sr", PS(Cap((string)s[1], 50)));
                                            q.Parameters.AddWithValue("@cl", v[1]);
                                            q.Parameters.AddWithValue("@rem", PS(callRemark));
                                            q.Parameters.AddWithValue("@cp", contactperson);
                                            q.Parameters.AddWithValue("@cb", MigrationUser);
                                            q.Parameters.AddWithValue("@con", createdon);
                                            q.Parameters.AddWithValue("@uon", done ? v[1] : updatedon);
                                            q.ExecuteNonQuery();
                                        }
                                        k++; pmsN++; if (done) pmsClosedN++;
                                    }
                                }
                            }
                        }

                    int billN = 0;
                    List<object[]> bills;
                    if (_billsByKey.TryGetValue(key, out bills))
                    {
                        string billPrefix = "BILL_" + ("0000000000" + contractno).Substring(("0000000000" + contractno).Length - 10);
                        foreach (object[] bl in bills)
                        {
                            using (SqlCommand q = new SqlCommand("INSERT INTO billingcycle (amccode, billingno, billingdate, amount, type, createdby, createdon, updatedby, updatedon) VALUES (@a, @no, @dt, @amt, 'BILL', @cb, @con, @cb, @uon)", tgt, tx))
                            {
                                q.Parameters.AddWithValue("@a", code);
                                q.Parameters.AddWithValue("@no", billPrefix + "_" + (billN + 1));
                                q.Parameters.AddWithValue("@dt", bl[0]);
                                q.Parameters.AddWithValue("@amt", bl[1]);
                                q.Parameters.AddWithValue("@cb", MigrationUser);
                                q.Parameters.AddWithValue("@con", createdon);
                                q.Parameters.AddWithValue("@uon", updatedon);
                                q.ExecuteNonQuery();
                            }
                            billN++;
                        }
                    }

                    tx.Commit();
                    _amcBySrc[key] = code;
                    for (int pi = 0; pi < prevContracts.Count; pi++) _renewals.Add(new[] { code, office, prevContracts[pi], pi == 0 ? 1 : 0 });
                    _rows++; _lines += lineN; _pms += pmsN; _pmsClosed += pmsClosedN; _bills += billN; if (modulecode != DBNull.Value) _quoteLinked++;
                    if (_rows % 1000 == 0) Console.WriteLine("  ... " + _rows + " AMC Contracts migrated");
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // Renewal: every previous contract of a renewed contract gets renew = 1 (296 eBizWiz contracts merge several old ones);
        // oldcontractcode (one column) = the first previous contract found; each line's remark names its own previous contract.
        // A contract pointing to itself (10 in eBizWiz) is ignored. - Aftab Alam
        static void LinkRenewals(SqlConnection tgt)
        {
            HashSet<int> linked = new HashSet<int>();
            foreach (int[] r in _renewals)
            {
                int old; if (!_amcBySrc.TryGetValue(r[1] + "|" + r[2], out old) || old == r[0]) continue;
                using (SqlCommand u = new SqlCommand("UPDATE amc SET renew=1 WHERE code=@old", tgt)) { u.Parameters.AddWithValue("@old", old); u.ExecuteNonQuery(); }
                if (!linked.Contains(r[0]))
                {
                    using (SqlCommand u = new SqlCommand("UPDATE amc SET oldcontractcode=@old WHERE code=@new", tgt))
                    { u.Parameters.AddWithValue("@old", old); u.Parameters.AddWithValue("@new", r[0]); u.ExecuteNonQuery(); }
                    linked.Add(r[0]); _renewLinked++;
                }
            }
        }

        // status module 'AMC': the app looks up name 'Open' (new PMS/complaint) and behavior 'Completed' (close) — same as the Cona reference.
        static void EnsureStatuses(SqlConnection tgt)
        {
            _stOpen = AmcStatus(tgt, "Open", "Pending", 1);
            _stClosed = AmcStatus(tgt, "Closed", "Completed", 0);
        }

        static int AmcStatus(SqlConnection tgt, string name, string behavior, int makedefault)
        {
            using (SqlCommand q = new SqlCommand("SELECT TOP 1 code FROM status WHERE module='AMC' AND name=@n ORDER BY code", tgt))
            { q.Parameters.AddWithValue("@n", name); object o = q.ExecuteScalar(); if (o != null && o != DBNull.Value) return Convert.ToInt32(o); }
            using (SqlCommand ins = new SqlCommand("INSERT INTO status (name, module, behavior, sort, makedefault, createdby, createdon, updatedby, updatedon) OUTPUT INSERTED.code VALUES (@n, 'AMC', @b, 0, @d, @cb, GETDATE(), @cb, GETDATE())", tgt))
            {
                ins.Parameters.AddWithValue("@n", name); ins.Parameters.AddWithValue("@b", behavior); ins.Parameters.AddWithValue("@d", makedefault); ins.Parameters.AddWithValue("@cb", MigrationUser);
                return Convert.ToInt32(ins.ExecuteScalar());
            }
        }

        static string Dt(object v) { return v == DBNull.Value || v == null ? "" : Convert.ToDateTime(v).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture); }

        static void LoadSource()
        {
            // Product lines by line code. A serial belongs to the contract in its OWN ncontr (eBizWiz header item total follows
            // trdcontr2itemsdet.ncontr; 20 serial rows point to a product line of another contract) - product / sign-up info from its line.
            Dictionary<string, object[]> itemByLine = new Dictionary<string, object[]>();
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncontr, ncode, nitem, ncontractis, npreviousno FROM trdcontr1items WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
                itemByLine[Int(r["nofficeid"]) + "|" + Int(r["ncode"])] = new object[] { Int(r["ncode"]), Int(r["nitem"]), Int(r["ncontractis"]), Int(r["npreviousno"]) };
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhcontr WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
                _contrNo[Int(r["nofficeid"]) + "|" + Int(r["ncode"])] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            // { 0 ncode, 1 serial, 2 location, 3 contrtype, 4 rate, 5 discpct, 6 start, 7 end, 8 months, 9 pmvisits, 10 masterrate, 11 closed, 12 first install }
            foreach (DataRow r in GetSrc(@"SELECT nofficeid, ncontr, ncontr1, ncode, vserialno, vlocation, ncontrtype, nrate, ndiscountperc, dstartdate, denddate, nmonths, npmvisits, nmasterrate, bclosed, dfirstinstdate
                                           FROM trdcontr2itemsdet WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, ncontr, ncontr1, ncode").Tables[0].Rows)
            {
                object[] line; string ck = Int(r["nofficeid"]) + "|" + Int(r["ncontr"]);
                if (!itemByLine.TryGetValue(Int(r["nofficeid"]) + "|" + Int(r["ncontr1"]), out line)) { _skipLines++; continue; }   // 1 orphan serial row in eBizWiz
                List<object[]> li; if (!_itemsByKey.TryGetValue(ck, out li)) _itemsByKey[ck] = li = new List<object[]>();
                if (!li.Any(x => (int)x[0] == (int)line[0])) li.Add(line);
                string k = ck + "|" + Int(r["ncontr1"]);
                List<object[]> l; if (!_serialsByLine.TryGetValue(k, out l)) _serialsByLine[k] = l = new List<object[]>();
                l.Add(new object[] { Int(r["ncode"]), Str(r["vserialno"]), Str(r["vlocation"]), Int(r["ncontrtype"]), Dec(r["nrate"]), Dec(r["ndiscountperc"]),
                                     r["dstartdate"] == DBNull.Value ? null : (object)Convert.ToDateTime(r["dstartdate"]), r["denddate"] == DBNull.Value ? null : (object)Convert.ToDateTime(r["denddate"]),
                                     Int(r["nmonths"]), Int(r["npmvisits"]), Dec(r["nmasterrate"]), Bool(r["bclosed"]), P(r["dfirstinstdate"]) });
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncontr, dbilldate, nbillamount FROM trdcontr3bills WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, ncontr, dbilldate, ncode").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["ncontr"]);
                List<object[]> l; if (!_billsByKey.TryGetValue(k, out l)) _billsByKey[k] = l = new List<object[]>();
                l.Add(new object[] { P(r["dbilldate"]), Dec(r["nbillamount"]) });
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncontr2, dschpmdate, dactpmdate, ncalls FROM trdcontr7pmvisit WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") ORDER BY nofficeid, ncontr2, dschpmdate, ncode").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["ncontr2"]);
                List<object[]> l; if (!_visitsBySerial.TryGetValue(k, out l)) _visitsBySerial[k] = l = new List<object[]>();
                l.Add(new object[] { P(r["dschpmdate"]), P(r["dactpmdate"]), Int(r["ncalls"]) });
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhoffer WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
                _quoteRef[Int(r["nofficeid"]) + "|" + Int(r["ncode"])] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno, dtrndate FROM trhsales WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string k = Int(r["nofficeid"]) + "|" + Int(r["ncode"]);
                _salesNo[k] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim(); _salesDt[k] = P(r["dtrndate"]);
            }
            foreach (DataRow r in GetSrc("SELECT nofficeid, ncode, vtrnprefix, ntrnno FROM trhcalls WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
                _callNo[Int(r["nofficeid"]) + "|" + Int(r["ncode"])] = (Str(r["vtrnprefix"]) + Str(r["ntrnno"])).Trim();
            Console.WriteLine("  Source: contracts with lines " + _itemsByKey.Count + ", serial groups " + _serialsByLine.Count + ", bill groups " + _billsByKey.Count + ", PM visit groups " + _visitsBySerial.Count);
        }

        static void BuildMaps(SqlConnection tgt)
        {
            foreach (int office in Offices)
            {
                string nm; string place = _officeName.TryGetValue(office, out nm) && nm.Length > 0 ? Cap(nm, 100) : "Office " + office;
                using (SqlCommand q = new SqlCommand("SELECT code FROM companyaddress WHERE ccode=@cc AND place=@pl", tgt))
                { q.Parameters.AddWithValue("@cc", CompanyCode); q.Parameters.AddWithValue("@pl", place); object o = q.ExecuteScalar(); if (o != null && o != DBNull.Value) _branchByOffice[office] = Convert.ToInt32(o); }
            }
            Dictionary<string, int> cByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM contact WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !cByName.ContainsKey(n)) cByName[n] = Int(r["code"]); }
            foreach (DataRow r in GetSrc("SELECT ncode, ntitle, vname FROM mstparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            {
                string k = NKey(r["ncode"]); if (k.Length == 0) continue;
                string nm = Cap(Join(LookInt(_fixed, Int(r["ntitle"])), Str(r["vname"])), 150);
                int c; if (nm.Length > 0 && cByName.TryGetValue(nm, out c) && !_custByParty.ContainsKey(k)) _custByParty[k] = c;
            }
            foreach (KeyValuePair<string, int> kv in PartyToContact) _custByParty[kv.Key] = kv.Value;
            foreach (DataRow r in GetTgt("SELECT code, ccode, isnull(isdefault,0) as isdefault FROM mltaddress WITH (NOLOCK) ORDER BY ccode, isdefault DESC, code").Tables[0].Rows)
            { int cc = Int(r["ccode"]); if (cc > 0 && !_branchByContact.ContainsKey(cc)) _branchByContact[cc] = Int(r["code"]); }
            Dictionary<string, int> pByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, name FROM product WITH (NOLOCK) ORDER BY isdeleted").Tables[0].Rows) { string n = Str(r["name"]); if (n.Length > 0 && !pByName.ContainsKey(n)) pByName[n] = Int(r["code"]); }
            foreach (DataRow r in GetSrc("SELECT ncode, vname FROM mstitems WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ")").Tables[0].Rows)
            { string n = Cap(Str(r["vname"]), 150); int c; if (n.Length > 0 && pByName.TryGetValue(n, out c) && !_prodByItem.ContainsKey(Int(r["ncode"]))) _prodByItem[Int(r["ncode"])] = c; }
            // AMC quotations migrated by module 9 (cors CQ, modulename 'AMC Contract')
            foreach (DataRow r in GetTgt("SELECT code, branchcode, inqref FROM inqcs WHERE cors='CQ' AND modulename='AMC Contract' AND inqref IS NOT NULL").Tables[0].Rows)
            { string k = Int(r["branchcode"]) + "|" + Str(r["inqref"]); if (!_cqByKey.ContainsKey(k)) _cqByKey[k] = Int(r["code"]); }
            // contact persons (party|person -> mltcontact.code), same resolution as the quotation modules
            Dictionary<string, int> mltMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in GetTgt("SELECT code, ccode, name FROM mltcontact WITH (NOLOCK)").Tables[0].Rows)
            { int code = Int(r["code"]), cc = Int(r["ccode"]); string name = Str(r["name"]); if (code > 0 && cc > 0 && name.Length > 0) { string k = cc + "|" + name; if (!mltMap.ContainsKey(k)) mltMap[k] = code; } }
            foreach (DataRow dr in GetSrc("SELECT nparty, ncode, ntitle, vcontactperson FROM msdparty WITH (NOLOCK) WHERE nofficeid IN (" + OfficeIn + ") AND LTRIM(RTRIM(ISNULL(vcontactperson,''))) <> ''").Tables[0].Rows)
            {
                string partyKey = NKey(dr["nparty"]); int personNo = Int(dr["ncode"]); if (partyKey.Length == 0 || personNo <= 0) continue;
                string key = partyKey + "|" + personNo; if (_persons.ContainsKey(key)) continue;
                string raw = Str(dr["vcontactperson"]); string formatted = Cap(Join(LookInt(_fixed, Int(dr["ntitle"])), raw), 150);
                int cc2, m1, m2;
                if (_custByParty.TryGetValue(partyKey, out cc2))
                {
                    if (mltMap.TryGetValue(cc2 + "|" + formatted, out m1)) _persons[key] = m1;
                    else if (mltMap.TryGetValue(cc2 + "|" + raw, out m2)) _persons[key] = m2;
                }
            }
        }

        static string Join(string a, string b) { return string.Join(" ", new[] { a, b }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(); }
    }
}
