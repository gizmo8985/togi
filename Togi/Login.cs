﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using System.Resources;
using System.Globalization;
using System.Reflection;
using TogiApi;
using TogiApi.Tools;
using TimeLineControl;
using System.Drawing;

namespace Togi
{
    public partial class Login : Form
    {

        public User LoginUser{get;set;}
        public IList<TweetItem> FriendsTimeLine{get;set;}

        private Thread lng;
        private ResourceManager dil_;
        private CultureInfo cInfo_;
        private bool mouse_is_down;
        private Point mouse_pos;

        private bool _ChangeUser;
        private string ScreenName;
        private string Password;

        delegate void SetText(string Yazi, Label Kontrol);
        delegate void SetVisible(Panel p, bool Display);

        public Login(bool changeuser)
        {            
            InitializeComponent();

            _ChangeUser = changeuser;
            cInfo_ = Tools.Setup.GetCultureInfo();
            Thread.CurrentThread.CurrentUICulture = cInfo_;
            dil_ = Tools.Setup.GetResourceManager();

            LanguageCtor();

            FriendsTimeLine = new List<TweetItem>();

            // write registry default values
            Tools.Setup ChkSetup = new Togi.Tools.Setup();
            ChkSetup.LoadRegistry();

            if (isSave(out ScreenName, out Password))
            {
                // Checking Connection
                if (Utils.IsConnected())
                {
                    btTry.Visible = false;
                    LoginStart();
                }
                else
                {
                    SetPanelVisibility(P1, false);
                    SetPanelVisibility(P2, true);
                    lLoading.Text = dil_.GetString("LOGIN_MESSAGE_1", cInfo_);
                    pLoadingIcon.Visible = false;
                    btTry.Text = dil_.GetString("LOGIN_BUTTON_2", cInfo_);
                    btTry.Visible = true;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(tSname.Text))
            {
                tSname.Focus();
                return;
            }

            if (String.IsNullOrEmpty(tPass.Text))
            {
                tPass.Focus();
                return;
            }

            ScreenName = tSname.Text;
            Password = tPass.Text;

            if(_ChangeUser)
                ChangeUserFlow();

            LoginStart();
        }

        private void LoginStart()
        {
            SetPanelVisibility(P1, false);
            SetPanelVisibility(P2, true);
            
            lng = new Thread(TwitterLogin);
            lng.SetApartmentState(ApartmentState.STA);
            lng.Start();
        }

        private void TwitterLogin()
        {
            try
            {
                string SinceId = Regedit.GetKey_("since_recent");
                Twitter login = new Twitter(ScreenName, Password);
                
                // TimeLine geliyor
                SetTextBoxText(dil_.GetString("LOGIN_LOADING_1", cInfo_), lLoading);

                //1. Okunmamislar Aliniyor. Since_id
                LoadTweetItem(login.FriendsTimeLine(SinceId), false);

                //2. SinceId Yenileniyor
                if(FriendsTimeLine.Count > 0)
                    login.SetSinceId(FriendsTimeLine[0].ItemTweet);

                //3. Okunmuslar Aliniyor. Max_id
                if(!String.IsNullOrEmpty(SinceId))
                    LoadTweetItem(login.FriendsTimeLine(SinceId, true), 
                        true);                

                //4. User Bilgileri Aliniyor.
                SetTextBoxText(dil_.GetString("LOGIN_LOADING_2", cInfo_), lLoading);

                LoginUser = login.ShowUser(ScreenName);
                LoginUser.UserName = ScreenName;
                LoginUser.UserPass = Password;

                // Hesap Regedit'e yazılyor.
                if (cRemember.Checked)
                    RememberThisAccount(ScreenName, Password);

                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                SetTextBoxText(ex.Message, lLoading);
                Thread.Sleep(4000);

                SetPanelVisibility(P1, true);
                SetPanelVisibility(P2, false);
            }
        }

        private void LanguageCtor()
        {
            lScreenName.Text = dil_.GetString("LOGIN_LABEL_1");
            lPassword.Text = dil_.GetString("LOGIN_LABEL_2");
            cRemember.Text = dil_.GetString("LOGIN_LABEL_3");
            btLogin.Text = dil_.GetString("LOGIN_BUTTON_1");
        }

        private void LoadTweetItem(IList<Tweet> liste,bool isRead)
        {
            lock (this)
            {
                foreach (Tweet item in liste)
                {
                    item.isRead = isRead;
                    TweetItem ti = new TweetItem(item);
                    FriendsTimeLine.Add(ti);
                }

            }
        }

        private void lClose_Click(object sender, EventArgs e)
        {
            if (_ChangeUser)
            {
                // Change User Cancel
                this.DialogResult = DialogResult.Cancel;                
            }
            else
            {
                // Sonlandır
                this.DialogResult = DialogResult.Abort;
            }
        }

        private void SetTextBoxText(string Yazi, Label Kontrol)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new SetText(SetTextBoxText), new object[] { Yazi, Kontrol });
                return;
            }

            Kontrol.Text = Yazi;
        }

        private void SetPanelVisibility(Panel p, bool Display)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new SetVisible(SetPanelVisibility), new object[] { p, Display });
                return;
            }

            p.Visible = Display;
        }

        private void RememberThisAccount(string lUser, string lPass)
        {
            string EncodePass;
            string EncodeScreenName;

            Crypto encode_ = new Crypto();
            EncodePass = encode_.EncryptString(lUser);
            EncodeScreenName = encode_.EncryptString(lPass);

            Regedit.SetKey_("login_name", EncodePass);
            Regedit.SetKey_("login_pass", EncodeScreenName);
        }

        private bool isSave(out string lUser, out string lPass)
        {
            bool CheckSave;
            CheckSave = false;

            Crypto decode_ = new Crypto();

            lUser = Regedit.GetKey_("login_name");
            lPass = Regedit.GetKey_("login_pass");

            if (!String.IsNullOrEmpty(lUser) && !String.IsNullOrEmpty(lPass))
            {
                lUser = decode_.DecryptString(lUser);
                lPass = decode_.DecryptString(lPass);
                CheckSave = true;
            }

            return CheckSave;
        }

        private void ChangeUserFlow()
        {
            Regedit.SetKey_("login_name", String.Empty);
            Regedit.SetKey_("login_pass", String.Empty);

            Regedit.SetKey_("since_message", String.Empty);
            Regedit.SetKey_("since_recent", String.Empty);
            Regedit.SetKey_("since_reply", String.Empty);
        }

        private void Login_MouseDown(object sender, MouseEventArgs e)
        {
            this.mouse_pos.X = e.X;
            this.mouse_pos.Y = e.Y;
            this.mouse_is_down = true;
        }

        private void Login_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.mouse_is_down)
            {
                Point mousePosition = Control.MousePosition;
                mousePosition.X -= this.mouse_pos.X;
                mousePosition.Y -= this.mouse_pos.Y;
                base.Location = mousePosition;
            }
        }

        private void Login_MouseUp(object sender, MouseEventArgs e)
        {
            this.mouse_is_down = false;
        }

        private void btTry_Click(object sender, EventArgs e)
        {
            this.btTry.Enabled = false;
            if (Utils.IsConnected())
            {
                btTry.Visible = false;
                LoginStart();
            }

            this.btTry.Enabled = true;
        }
    }
}
