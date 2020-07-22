using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using EasyModbus;
using System.IO.Ports;
using System.Threading;

namespace WindowsFormsApp5
{
    

    public partial class Form1 : Form
    {
        SerialPort port;
        string lineReadIn;
        bool flaga = false;
        

        // this will prevent cross-threading between the serial port
        // received data thread & the display of that data on the central thread
        private delegate void preventCrossThreading(string x);
        private preventCrossThreading accessControlFromCentralThread;

        public Form1()
        {
            InitializeComponent();
            textBox2.Select();
            // create and open the serial port (configured to my machine)
            // this is a Down-n-Dirty mechanism devoid of try-catch blocks and
            // other niceties associated with polite programming
            const string com = "COM8";
            port = new SerialPort(com, 9600, Parity.None, 8, StopBits.One);

            //   port.ErrorReceived += new SerialErrorReceivedEventHandler();
            try
            {
                port.Open();
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show("Error: Port " + com + " jest zajęty");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Uart exception: " + ex);
            }


 

            if (port.IsOpen)
            {
                // set the 'invoke' delegate and attach the 'receive-data' function
                // to the serial port 'receive' event.
                accessControlFromCentralThread = displayTextReadIn;
                port.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
            }
        }

      

        private void button1_Click(object sender, EventArgs e)
        {
        //    int errorcode = 0;
        //    port.Close();
            //Application.Restart();
            textBox2.Clear();
            RCVbox.Clear();
            flaga = false;
            label1.Text = "Zeskanuj barcode";
            label1.BackColor = DefaultBackColor;

            wyjscie = true;
            //     Application.Restart();
            //     Environment.Exit(errorcode);
            textBox2.ReadOnly = false;
            textBox2.Select();


        }

        private void zerowanieRS()
        {
            flaga = false;
            // clear the RCVbox text string and write the VER command
            RCVbox.Text = lineReadIn = string.Empty;
            port.Write("VER\r");
        }



        // this is called when the serial port has receive-data for us.
        private void port_DataReceived(object sender, SerialDataReceivedEventArgs rcvdData)
        {

            while (port.BytesToRead > 0)
            {
                lineReadIn += port.ReadExisting();
                Thread.Sleep(25);
            }

            // display what we've acquired.
            if (lineReadIn == "OK")
            {
                flaga = true;                   //Jesli program odbierze sygnał: "OK" zmienia wartosc flagi na true
              //  lineReadIn = string.Empty;
            }

            displayTextReadIn(lineReadIn);
            lineReadIn = string.Empty;
        }// end function 'port_dataReceived'



        // this, hopefully, will prevent cross threading.
        private void displayTextReadIn(string ToBeDisplayed)          //wyswietlanie sygnalu na drugim texboxie
        {
            if (RCVbox.InvokeRequired)
                RCVbox.Invoke(accessControlFromCentralThread, ToBeDisplayed);
            else
                RCVbox.Text = ToBeDisplayed;

        }





        int TypTestera = MOJ; //T_MY_PC
        bool wyjscie = false;

        DateTime start;
        DateTime stop;
        //---------------------------------------------------------------------------------------------

        
        const int M_NIENARODZONY = 1;
        const int M_BRAK_KROKU = 2;
        const int M_FAIL = 3;
        const int M_BRAK_POLACZENIA_Z_MES = 4;
        const int T_MY_PC = 5; //typtestera
        const int T_BRIDLE = 1;
        const int T_SHELL = 2;
        const int T_PRERUN = 3;
        const int T_VALVE = 4;
        const int MOJ = 0;
        string TxtTestProcess;
        string TxtTestType;


        public int Test1(string SerialTxt)
        {
            using (MESwebservice.BoardsSoapClient wsMES = new MESwebservice.BoardsSoapClient("BoardsSoap"))
            {
                DataSet Result;
                try
                {
                    Result = wsMES.GetBoardHistoryDS(@"itron", SerialTxt);
                }
                catch
                {
                    return M_BRAK_POLACZENIA_Z_MES;
                }

                var Test = Result.Tables[0].TableName;
                if (Test != "BoardHistory") return M_NIENARODZONY; //numer produktu nie widnieje w systemie MES

                switch (TypTestera)
                {
                    case T_BRIDLE:
                        TxtTestProcess = "Link / LINK_BB1".ToUpper();
                        TxtTestType = "Movement Both".ToUpper();
                        MessageBox.Show("TxtTestProcess:", TxtTestProcess, MessageBoxButtons.OK);
                        break;

                    case T_SHELL:
                        TxtTestProcess = "QC / TEST_VERIFY".ToUpper();
                        TxtTestType = "TEST".ToUpper();
                        break;

                    case T_PRERUN:
                        TxtTestProcess = "QC / SILICON".ToUpper();
                        TxtTestType = "TEST".ToUpper();
                        break;

                    case T_VALVE:
                        TxtTestProcess = "QC / CHECK_PRERUN".ToUpper();
                        TxtTestType = "TEST".ToUpper();
                        break;

                 
                    case T_MY_PC:
                        TxtTestProcess = "QC / SILICON".ToUpper();
                        TxtTestType = "TEST".ToUpper();
                        break;
                    default:
                    case MOJ:
                        TxtTestProcess = "QC / FVT_SZ_VALVE".ToUpper();
                        TxtTestType = "TEST".ToUpper();
                        break;
                    

                }

                var data = (from row in Result.Tables["BoardHistory"].AsEnumerable()
                            where row.Field<string>("Test_Process").ToUpper() == TxtTestProcess && row.Field<string>("TestType").ToUpper() == TxtTestType
                            select new
                            {
                                TestProcess = row.Field<string>("Test_Process"),
                                TestType = row.Field<string>("TestType"),
                                TestStatus = row.Field<string>("TestStatus"),
                                StartDateTime = row.Field<DateTime>("StartDateTime"),
                                StopDateTime = row.Field<DateTime>("StopDateTime"),
                            }).FirstOrDefault();


                if (data != null)
                {                
                    //sprawdzamy PASS w poprzednim kroku
                    if ("PASS" == data.TestStatus.ToUpper()) return 0; //wszystko jest OK
                    else return M_FAIL;
                }
                else return M_BRAK_KROKU; //brak poprzedniego kroku
            }
        }




        private int sprawdzeniekrok(string sn)
        {
            int Result;
            
            Result = Test1(sn); //przykladowy numer seryjny 9100000668
            switch (Result)
            {
                case M_BRAK_POLACZENIA_Z_MES:
                    MessageBox.Show("Brak połączenia z MES.", "Info", MessageBoxButtons.OK);
                    label1.Text = "Brak połączenia z MES.";
                    break;

                case M_NIENARODZONY:
                    MessageBox.Show("Numer nienarodzony w MES.", "Info", MessageBoxButtons.OK);
                    label1.Text = "Numer nienarodzony w MES.";
                    break;

                case M_BRAK_KROKU:
                    MessageBox.Show("Brak poprzedniego kroku.", "Info", MessageBoxButtons.OK);
                    label1.Text = "Brak poprzedniego kroku.";
                    break;

                case M_FAIL:
                    MessageBox.Show("Poprzedni krok = FAIL.", "Info", MessageBoxButtons.OK);
                    label1.Text = "Poprzedni krok = FAIL.";
                    break;

                default:
                   // MessageBox.Show("Wszystko jest OK", "Info", MessageBoxButtons.OK);                   
                    return 1;
            }
            label1.BackColor = Color.Red;
            return 0;
        }


        private void tworzeniepliku(string sn)
        {
            string sciezka = ("C:/tars/");      //definiowanieścieżki do której zapisywane logi

            if (Directory.Exists(sciezka))       //sprawdzanie czy  istnieje
            {
                ;
            }
            else
                System.IO.Directory.CreateDirectory(sciezka); //jeśli nie to ją tworzy

            using (StreamWriter sw = new StreamWriter("C:/tars/" + sn + "(" + stop.Day + "-" + stop.Month + "-" + stop.Year  + " "+ stop.Hour + "-" + stop.Minute + "-" + stop.Second + ")" + ".Tars"))
            {


                sw.WriteLine("S{0}", sn);
                sw.WriteLine("CITRON");
                sw.WriteLine("NPLKWIM0P25B1M22");
                sw.WriteLine("PQC_SCREW");
                sw.WriteLine("Ooperator");
                sw.WriteLine("[" + start.Year + "-" + start.Month + "-" + start.Day + " " + start.Hour + ":" + start.Minute + ":" + start.Second);
                sw.WriteLine("]" + stop.Year + "-" + stop.Month + "-" + stop.Day + " " + stop.Hour + ":" + stop.Minute + ":" + stop.Second);
                sw.WriteLine("TP");

            }


        }

    //   private void OnKeyDownHandler(object sender, KeyEventArgs e)
    //   {
     //  //     textBox2.Select();
       //           if (e.KeyCode == Keys.Enter)
       //         textBox2.Select();  
            //           textBox2.Text = "You Entered: " + textBox2.Text;
            //             MessageBox.Show("Enter", "Info", MessageBoxButtons.OK);
            //         }
      //  }


        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            //textBox2.MaxLength = 10;
            

            if (textBox2.TextLength == 10) //jesli w texboxie jest 10 znakow
            {
                string serialnumber = textBox2.Text.Trim(); //przycina białe znaki
               // textBox2.ReadOnly = true;

                if (sprawdzeniekrok(@serialnumber) == 1) //sprawdza historię jeśli ma poprzednie kroki to funkcja sprawdzaniekrok zwróci 1
                {
                    textBox2.ReadOnly = true;
                    label1.BackColor = DefaultBackColor;
                    start = DateTime.Now;                  //zwraca date rozpoczecia testu
                    zerowanieRS();                       //zeruje dane wysyłane po RS
                    wyjscie = false;
                   while (flaga == false && wyjscie == false)
                   {
                        label1.Text = "Wkręć śubę";
                        Application.DoEvents();           //służy do obsługi zdarzeń poza pętlą (zmienia wartość label1 i obsługa przycisków)
                        Thread.Sleep(500);                //opóźnienie 0,5s czekamy na wkręcenie śruby
                        if (flaga)
                           break;                        //   if (flaga)
                                                          //       break;
                                                          //   label1.Text = "wkręć śubę";
                                                          //  System.Windows.Forms.DialogResult Show = MessageBox.Show("Wkreć śrubę", "Info", MessageBoxButtons.OKCancel);

                        //    if (Show == System.Windows.Forms.DialogResult.Cancel)
                        //     {
                        //        textBox2.Clear();
                        //      MessageBox.Show("Przerwano operację, zacznij od nowa", "Info", MessageBoxButtons.OK);
                        //      break;
                        //  }4000199092



                   }

                    if (flaga == true)
                    {
                        label1.Text = "Moment OK";
                        RCVbox.Text = "OK";
                        label1.BackColor = Color.Lime;
                        //   MessageBox.Show("tworzy plik", "Info", MessageBoxButtons.OK);
                        stop = DateTime.Now;
                        tworzeniepliku(serialnumber);
                        Application.DoEvents();
                        Thread.Sleep(5000);
                        textBox2.Clear();
                        textBox2.ReadOnly = false;
                        label1.Text = "Zeskanuj barcode";
                        label1.BackColor = DefaultBackColor;
                    }
                }
                else
                {
                  //  label1.Text = "Brak loga, zeskanuj jeszcze raz barcode";
                  //  label1.BackColor = Color.Red;
                    Application.DoEvents();
                   Thread.Sleep(500);
                   // label1.BackColor = DefaultBackColor;
                    textBox2.Clear();
                    textBox2.ReadOnly = false;
                   // label1.Text = "Zeskanuj barcode";
                }

                }
            else if (textBox2.TextLength > 10)
            {
                textBox2.Clear();
                textBox2.Text = string.Empty;
                textBox2.ReadOnly = false;
                label1.Text = "Zeskanuj barcode";
            }
            
            

        }




        //       private void button2_Click(object sender, EventArgs e)
        //       {
        //          textBox2.Clear();
        //            textBox2.Text = string.Empty;

        //       }


    }
}
