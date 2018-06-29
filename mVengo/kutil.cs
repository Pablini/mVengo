using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;


namespace Invengo101

{
    class kutil
    {
        static string m_connstr = "server=localhost;port=3310;database=cardetector_fixed_lpr;user=toor;password=ckseller";
        static Dictionary<string, string> reads_vin = new Dictionary<string, string>();


        //Numbers
        //static Regex regex = new Regex("^[0-9]+$");
        static Regex rgx = new Regex("[^a-zA-Z0-9 -]");


        public static string getVIN(string rawhex)
        {
            string hexserie = rawhex.Replace(" ", string.Empty);
            string serie = "";


            if (rawhex == "")
            {
                return "00000000000000000";
            }
            //Console.WriteLine("REPUVE HEX-VIN:" + hexserie);

            for (int i = 0; i < 18; i++)
            {
                string hex = hexserie.Substring(i * 2, 2);
                int num = int.Parse(hex, System.Globalization.NumberStyles.AllowHexSpecifier);
                char cnum = (char)num;
                serie += cnum;
            }

            string resp = rgx.Replace(serie, "");
            resp = resp.Replace(" ", string.Empty);
            return resp;
        }

        public static string getRNC(string rawhex)
        {
            string output = "";
            string str = "";
            int p;

            string hexserie = rawhex.Replace(" ", string.Empty);
            for (int i = 0; i < hexserie.Length; i += 2)
            {
                str = hexserie.Substring(i, 2);
                bool success = int.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, out p);
                if (success) output += (char)p;
            }

            string resp = rgx.Replace(output, "");
            resp = resp.Replace(" ", string.Empty);
            return resp;
        }


        public static bool InsRFID(DateTime timex, int dev, int signal, string vin, string tagid)
        {
            //  INSERT INTO rfid.log VALUES(0,63611958428134,"2016-10-13T12:27:08.134",60,"ASDFGHJKLZ1234567890","1234567890");
            string sql = "INSERT INTO RFIDInfo(dev,tagid,datetime,vin,rssid) VALUES ("
                + dev + ","
                + "\'" + vin + "\',"
                + "\'" + timex.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "\',"
                + "\'" + vin + "\',"
                + signal
                + ")";

            MySqlConnection dbcon;
            MySqlCommand comm;

            dbcon = new MySqlConnection(m_connstr);
            //Console.WriteLine("DBCON:" + m_connstr);
            try
            {
                dbcon.Open();
                comm = dbcon.CreateCommand();
                comm.CommandText = sql;
                Console.WriteLine("SQL:"+sql);

                comm.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.ToString());
                Console.WriteLine(ex.ToString());
                return false;
            }
            finally
            {
                dbcon.Dispose();
            }
            return true;
        }



    }
}
