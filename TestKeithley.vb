Imports System
Imports System.Windows.Forms
Imports System.Drawing
Imports System.Net.Sockets
Imports System.Text
Imports Microsoft.VisualBasic

''' <summary>
''' Formulaire de test Keithley 2700/2701 — autonome, sans dépendance au reste du projet.
''' Permet de valider : connexion TCP, lecture de 2 voies TC, pilotage d'une sortie.
''' Pour l'utiliser : dans Program.vb, remplacer temporairement
'''   Application.Run(New FormPrincipal()) par Application.Run(New FormTestKeithley())
''' </summary>
Public Class FormTestKeithley
    Inherits Form

    ' ── Connexion ──
    Private _client    As TcpClient
    Private _stream    As NetworkStream
    Private _connecte  As Boolean = False

    ' ── Contrôles ──
    Private WithEvents _btnConnecter    As New Button()
    Private WithEvents _btnDeconnecter  As New Button()
    Private WithEvents _btnLire         As New Button()
    Private WithEvents _btnSortieOn     As New Button()
    Private WithEvents _btnSortieOff    As New Button()
    Private WithEvents _btnEffacerLog   As New Button()

    Private _txtIP      As New TextBox()
    Private _txtVoie1   As New TextBox()
    Private _txtVoie2   As New TextBox()
    Private _txtSortie  As New TextBox()
    Private _numTension As New NumericUpDown()
    Private _cmbTypeTC  As New ComboBox()
    Private _lblEtat    As New Label()
    Private _rtbLog     As New RichTextBox()

    ' ─────────────────────────────────────────────────────────────────────────

    Public Sub New()
        Me.Text            = "Test Keithley 2700 / 2701"
        Me.Size            = New Size(560, 680)
        Me.MinimumSize     = New Size(560, 680)
        Me.MaximumSize     = New Size(560, 680)
        Me.StartPosition   = FormStartPosition.CenterScreen
        Me.BackColor       = Color.FromArgb(245, 247, 252)
        Me.Font            = New Font("Segoe UI", 10)
        ConstruireUI()
    End Sub

    ' ─── Construction de l'interface ─────────────────────────────────────────

    Private Sub ConstruireUI()

        Dim x0 = 20   ' marge gauche
        Dim y  = 16
        Dim largeurLabel = 160
        Dim largeurChamp = 140
        Dim hauteurLigne = 36

        ' ══ SECTION 1 : CONNEXION ════════════════════════════════════════════

        AjouterSection("1 — Connexion à la centrale", x0, y) : y += 32

        ' Adresse IP
        AjouterLabel("Adresse IP :", x0, y + 4)
        _txtIP.Location = New Point(x0 + largeurLabel, y)
        _txtIP.Size     = New Size(largeurChamp, 28)
        _txtIP.Text     = "192.168.0.3"
        _txtIP.Font     = New Font("Consolas", 11)
        Me.Controls.Add(_txtIP)
        AjouterNote("Port 1394 (fixe)", x0 + largeurLabel + largeurChamp + 10, y + 6)
        y += hauteurLigne

        ' Boutons connexion
        _btnConnecter.Text      = "Connecter"
        _btnConnecter.Location  = New Point(x0 + largeurLabel, y)
        _btnConnecter.Size      = New Size(130, 32)
        _btnConnecter.BackColor = Color.FromArgb(55, 140, 60)
        _btnConnecter.ForeColor = Color.White
        _btnConnecter.FlatStyle = FlatStyle.Flat
        _btnConnecter.Font      = New Font("Segoe UI", 10, FontStyle.Bold)
        Me.Controls.Add(_btnConnecter)

        _btnDeconnecter.Text      = "Déconnecter"
        _btnDeconnecter.Location  = New Point(x0 + largeurLabel + 140, y)
        _btnDeconnecter.Size      = New Size(130, 32)
        _btnDeconnecter.BackColor = Color.FromArgb(160, 50, 40)
        _btnDeconnecter.ForeColor = Color.White
        _btnDeconnecter.FlatStyle = FlatStyle.Flat
        _btnDeconnecter.Enabled   = False
        Me.Controls.Add(_btnDeconnecter)
        y += 42

        ' Indicateur d'état
        _lblEtat.Text      = "⬤  Non connectée"
        _lblEtat.Location  = New Point(x0, y)
        _lblEtat.AutoSize  = True
        _lblEtat.ForeColor = Color.Gray
        _lblEtat.Font      = New Font("Segoe UI", 10, FontStyle.Bold)
        Me.Controls.Add(_lblEtat)
        y += 34

        AjouterSeparateur(y) : y += 16

        ' ══ SECTION 2 : LECTURE DES VOIES ════════════════════════════════════

        AjouterSection("2 — Lire 2 voies de mesure (thermocouples)", x0, y) : y += 32

        ' Voie 1
        AjouterLabel("N° voie 1 :", x0, y + 4)
        _txtVoie1.Location = New Point(x0 + largeurLabel, y)
        _txtVoie1.Size     = New Size(90, 28)
        _txtVoie1.Text     = "102"
        _txtVoie1.Font     = New Font("Consolas", 12, FontStyle.Bold)
        _txtVoie1.TextAlign = HorizontalAlignment.Center
        Me.Controls.Add(_txtVoie1)
        AjouterNote("ex : 101, 102 … 120  (carte 1)  ou  201 … 220  (carte 2)",
                    x0 + largeurLabel + 100, y + 6)
        y += hauteurLigne

        ' Voie 2
        AjouterLabel("N° voie 2 :", x0, y + 4)
        _txtVoie2.Location = New Point(x0 + largeurLabel, y)
        _txtVoie2.Size     = New Size(90, 28)
        _txtVoie2.Text     = "103"
        _txtVoie2.Font     = New Font("Consolas", 12, FontStyle.Bold)
        _txtVoie2.TextAlign = HorizontalAlignment.Center
        Me.Controls.Add(_txtVoie2)
        y += hauteurLigne

        ' Type TC
        AjouterLabel("Type thermocouple :", x0, y + 4)
        _cmbTypeTC.Location      = New Point(x0 + largeurLabel, y)
        _cmbTypeTC.Size          = New Size(90, 28)
        _cmbTypeTC.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbTypeTC.Items.AddRange({"K", "J", "T", "E", "N", "R", "S", "B"})
        _cmbTypeTC.SelectedIndex = 0
        Me.Controls.Add(_cmbTypeTC)
        AjouterNote("K = le plus courant (nichrome/alumel)", x0 + largeurLabel + 100, y + 6)
        y += hauteurLigne

        ' Bouton lire
        _btnLire.Text      = "▶  Configurer et lire les voies"
        _btnLire.Location  = New Point(x0 + largeurLabel, y)
        _btnLire.Size      = New Size(280, 36)
        _btnLire.BackColor = Color.FromArgb(40, 110, 175)
        _btnLire.ForeColor = Color.White
        _btnLire.FlatStyle = FlatStyle.Flat
        _btnLire.Font      = New Font("Segoe UI", 10, FontStyle.Bold)
        _btnLire.Enabled   = False
        Me.Controls.Add(_btnLire)
        y += 50

        AjouterSeparateur(y) : y += 16

        ' ══ SECTION 3 : SORTIE ANALOGIQUE ════════════════════════════════════

        AjouterSection("3 — Piloter une sortie analogique", x0, y) : y += 32

        ' Numéro sortie
        AjouterLabel("N° sortie :", x0, y + 4)
        _txtSortie.Location = New Point(x0 + largeurLabel, y)
        _txtSortie.Size     = New Size(90, 28)
        _txtSortie.Text     = "223"
        _txtSortie.Font     = New Font("Consolas", 12, FontStyle.Bold)
        _txtSortie.TextAlign = HorizontalAlignment.Center
        Me.Controls.Add(_txtSortie)
        AjouterNote("123 ou 124  (carte 1)  /  223 ou 224  (carte 2)", x0 + largeurLabel + 100, y + 6)
        y += hauteurLigne

        ' Tension
        AjouterLabel("Tension (0–12 V) :", x0, y + 4)
        _numTension.Location      = New Point(x0 + largeurLabel, y)
        _numTension.Size          = New Size(90, 28)
        _numTension.Minimum       = 0
        _numTension.Maximum       = 12
        _numTension.DecimalPlaces = 1
        _numTension.Increment     = CDec(0.5)
        _numTension.Value         = 5
        _numTension.Font          = New Font("Consolas", 11)
        Me.Controls.Add(_numTension)
        AjouterNote("5 V = ON standard  /  0 V = OFF", x0 + largeurLabel + 100, y + 6)
        y += hauteurLigne

        ' Boutons sortie
        _btnSortieOn.Text      = "⚡  Appliquer tension"
        _btnSortieOn.Location  = New Point(x0 + largeurLabel, y)
        _btnSortieOn.Size      = New Size(175, 36)
        _btnSortieOn.BackColor = Color.FromArgb(55, 140, 60)
        _btnSortieOn.ForeColor = Color.White
        _btnSortieOn.FlatStyle = FlatStyle.Flat
        _btnSortieOn.Font      = New Font("Segoe UI", 10, FontStyle.Bold)
        _btnSortieOn.Enabled   = False
        Me.Controls.Add(_btnSortieOn)

        _btnSortieOff.Text      = "✕  Mettre à 0 V"
        _btnSortieOff.Location  = New Point(x0 + largeurLabel + 185, y)
        _btnSortieOff.Size      = New Size(150, 36)
        _btnSortieOff.BackColor = Color.FromArgb(160, 50, 40)
        _btnSortieOff.ForeColor = Color.White
        _btnSortieOff.FlatStyle = FlatStyle.Flat
        _btnSortieOff.Enabled   = False
        Me.Controls.Add(_btnSortieOff)
        y += 50

        AjouterSeparateur(y) : y += 12

        ' ══ LOG ══════════════════════════════════════════════════════════════

        Dim lblLog As New Label() With {
            .Text      = "Journal :",
            .Location  = New Point(x0, y),
            .AutoSize  = True,
            .ForeColor = Color.FromArgb(80, 90, 120),
            .Font      = New Font("Segoe UI", 9, FontStyle.Bold)
        }
        Me.Controls.Add(lblLog)

        _btnEffacerLog.Text      = "Effacer"
        _btnEffacerLog.Location  = New Point(460, y - 2)
        _btnEffacerLog.Size      = New Size(76, 24)
        _btnEffacerLog.FlatStyle = FlatStyle.Flat
        _btnEffacerLog.Font      = New Font("Segoe UI", 8)
        Me.Controls.Add(_btnEffacerLog)
        y += 24

        _rtbLog.Location    = New Point(x0, y)
        _rtbLog.Size        = New Size(516, 165)
        _rtbLog.ReadOnly    = True
        _rtbLog.BackColor   = Color.FromArgb(20, 22, 34)
        _rtbLog.ForeColor   = Color.FromArgb(180, 190, 210)
        _rtbLog.Font        = New Font("Consolas", 9.5)
        _rtbLog.BorderStyle = BorderStyle.FixedSingle
        _rtbLog.ScrollBars  = RichTextBoxScrollBars.Vertical
        Me.Controls.Add(_rtbLog)

        ' ── Événements ──
        AddHandler _btnConnecter.Click,   AddressOf BtnConnecter_Click
        AddHandler _btnDeconnecter.Click, AddressOf BtnDeconnecter_Click
        AddHandler _btnLire.Click,        AddressOf BtnLire_Click
        AddHandler _btnSortieOn.Click,    AddressOf BtnSortieOn_Click
        AddHandler _btnSortieOff.Click,   AddressOf BtnSortieOff_Click
        AddHandler _btnEffacerLog.Click,  Sub(s, e) _rtbLog.Clear()
    End Sub

    ' ─── Connexion ────────────────────────────────────────────────────────────

    Private Sub BtnConnecter_Click(s As Object, e As EventArgs) Handles _btnConnecter.Click
        Dim ip = _txtIP.Text.Trim()
        If ip = "" Then
            Log("⚠  Saisissez une adresse IP.", Color.Orange) : Return
        End If
        Try
            Log("Connexion en cours vers " & ip & ":1394 …")
            Application.DoEvents()
            _client = New TcpClient()
            Dim res = _client.BeginConnect(ip, 1394, Nothing, Nothing)
            If Not res.AsyncWaitHandle.WaitOne(5000) Then
                Log("❌  Timeout — vérifiez que la centrale est allumée et l'IP correcte.", Color.FromArgb(220, 80, 70))
                Return
            End If
            _client.EndConnect(res)
            _stream             = _client.GetStream()
            _stream.ReadTimeout = 8000
            _connecte           = True

            _lblEtat.Text      = "⬤  Connectée — " & ip
            _lblEtat.ForeColor = Color.FromArgb(60, 180, 80)
            _btnConnecter.Enabled   = False
            _btnDeconnecter.Enabled = True
            _btnLire.Enabled        = True
            _btnSortieOn.Enabled    = True
            _btnSortieOff.Enabled   = True

            Log("✔  Connexion TCP établie.", Color.FromArgb(80, 210, 120))
            Envoyer("*RST")
            Log("   → *RST envoyé  (le bip de la centrale est normal)", Color.Gray)

        Catch ex As Exception
            Log("❌  Erreur : " & ex.Message, Color.FromArgb(220, 80, 70))
        End Try
    End Sub

    Private Sub BtnDeconnecter_Click(s As Object, e As EventArgs) Handles _btnDeconnecter.Click
        Try
            Envoyer("*RST")
            _stream.Close()
            _client.Close()
        Catch
        End Try
        _connecte = False
        _lblEtat.Text      = "⬤  Non connectée"
        _lblEtat.ForeColor = Color.Gray
        _btnConnecter.Enabled   = True
        _btnDeconnecter.Enabled = False
        _btnLire.Enabled        = False
        _btnSortieOn.Enabled    = False
        _btnSortieOff.Enabled   = False
        Log("Déconnectée.")
    End Sub

    ' ─── Lecture voies ────────────────────────────────────────────────────────

    Private Sub BtnLire_Click(s As Object, e As EventArgs) Handles _btnLire.Click
        Dim v1 = _txtVoie1.Text.Trim()
        Dim v2 = _txtVoie2.Text.Trim()
        Dim tc = _cmbTypeTC.Text

        If v1 = "" OrElse v2 = "" Then
            Log("⚠  Saisissez les deux numéros de voie.", Color.Orange) : Return
        End If

        Dim liste = v1 & "," & v2
        Log("── Configuration scan  (voies " & liste & ", TC type " & tc & ") ──")

        ' Séquence exacte de l'ancien code fonctionnel
        Envoyer("FORM:ELEM READ")
        Envoyer("TRAC:CLE")
        Envoyer("UNIT:TEMP C,(@" & liste & ")")
        Envoyer("FUNC 'TEMP',(@" & liste & ")")
        Envoyer("TEMP:TRAN TC,(@" & liste & ")")
        Envoyer("TEMP:TC:TYPE " & tc & ",(@" & liste & ")")
        Envoyer("SENS:TEMP:APER 0.05,(@" & liste & ")")
        Envoyer("INIT:CONT OFF")
        Envoyer("TRIG:COUN 1")
        Envoyer("SAMP:COUN 2")
        Envoyer("ROUT:SCAN (@" & liste & ")")
        Envoyer("ROUT:SCAN:TSO IMM")
        Envoyer("ROUT:SCAN:LSEL INT")

        Log("   → Vous devriez entendre les tac-tac de scrutation.", Color.Gray)
        Log("── Lecture (Read?) ──")

        ' 3 lectures successives pour valider
        For i As Integer = 1 To 3
            Dim rep = Lire("Read?")
            If rep = "" Then
                Log("   Lecture " & i & " → (pas de réponse)", Color.Orange)
            Else
                Dim vals = rep.Split(","c)
                If vals.Length >= 2 Then
                    Dim t1 As Double, t2 As Double
                    Dim ok1 = Double.TryParse(vals(0).Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, t1)
                    Dim ok2 = Double.TryParse(vals(1).Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, t2)
                    Log(String.Format("   Lecture {0} →  Voie {1} = {2}   |   Voie {3} = {4}",
                        i,
                        v1, If(ok1, t1.ToString("F3") & " °C", vals(0).Trim()),
                        v2, If(ok2, t2.ToString("F3") & " °C", vals(1).Trim())),
                        Color.FromArgb(140, 220, 255))
                Else
                    Log("   Lecture " & i & " → " & rep, Color.FromArgb(140, 220, 255))
                End If
            End If
        Next
    End Sub

    ' ─── Sortie analogique ────────────────────────────────────────────────────

    Private Sub BtnSortieOn_Click(s As Object, e As EventArgs) Handles _btnSortieOn.Click
        Dim numSortie = _txtSortie.Text.Trim()
        If numSortie = "" Then
            Log("⚠  Saisissez le numéro de sortie.", Color.Orange) : Return
        End If
        Dim tension = CDbl(_numTension.Value)
        Dim cmd = String.Format("OUTP:VOLT {0}, (@{1})",
            tension.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            numSortie)
        Envoyer(cmd)
        Log(String.Format("   → Sortie {0} = {1:F1} V  (ON)", numSortie, tension),
            Color.FromArgb(80, 210, 120))
    End Sub

    Private Sub BtnSortieOff_Click(s As Object, e As EventArgs) Handles _btnSortieOff.Click
        Dim numSortie = _txtSortie.Text.Trim()
        If numSortie = "" Then
            Log("⚠  Saisissez le numéro de sortie.", Color.Orange) : Return
        End If
        Dim cmd = "OUTP:VOLT 0.0, (@" & numSortie & ")"
        Envoyer(cmd)
        Log("   → Sortie " & numSortie & " = 0 V  (OFF)", Color.FromArgb(220, 100, 80))
    End Sub

    ' ─── Communication ────────────────────────────────────────────────────────

    Private Sub Envoyer(cmd As String)
        If Not _connecte Then Return
        Try
            Dim data = Encoding.ASCII.GetBytes(cmd & vbCr)
            _stream.Write(data, 0, data.Length)
        Catch ex As Exception
            Log("❌  Erreur envoi : " & ex.Message, Color.FromArgb(220, 80, 70))
        End Try
    End Sub

    Private Function Lire(cmd As String) As String
        If Not _connecte Then Return ""
        Try
            Dim data = Encoding.ASCII.GetBytes(cmd & vbCr)
            _stream.Write(data, 0, data.Length)
            Dim buffer(_client.ReceiveBufferSize - 1) As Byte
            Dim n = _stream.Read(buffer, 0, buffer.Length)
            Return Encoding.ASCII.GetString(buffer, 0, n).Trim()
        Catch ex As Exception
            Log("❌  Erreur lecture : " & ex.Message, Color.FromArgb(220, 80, 70))
            Return ""
        End Try
    End Function

    ' ─── Log ──────────────────────────────────────────────────────────────────

    Private Sub Log(msg As String, Optional couleur As Color = Nothing)
        Dim c = If(couleur = Color.Empty OrElse couleur = Nothing,
                   Color.FromArgb(180, 190, 210), couleur)
        _rtbLog.SelectionStart  = _rtbLog.TextLength
        _rtbLog.SelectionLength = 0
        _rtbLog.SelectionColor  = c
        _rtbLog.AppendText(DateTime.Now.ToString("HH:mm:ss")  & "  " & msg & Environment.NewLine)
        _rtbLog.SelectionColor  = _rtbLog.ForeColor
        _rtbLog.ScrollToCaret()
    End Sub

    ' ─── Helpers mise en page ─────────────────────────────────────────────────

    Private Sub AjouterSection(texte As String, x As Integer, y As Integer)
        Me.Controls.Add(New Label() With {
            .Text      = texte,
            .Location  = New Point(x, y),
            .AutoSize  = True,
            .Font      = New Font("Segoe UI", 10, FontStyle.Bold),
            .ForeColor = Color.FromArgb(40, 80, 160)
        })
    End Sub

    Private Sub AjouterLabel(texte As String, x As Integer, y As Integer)
        Me.Controls.Add(New Label() With {
            .Text      = texte,
            .Location  = New Point(x, y),
            .AutoSize  = True,
            .Font      = New Font("Segoe UI", 10),
            .ForeColor = Color.FromArgb(50, 55, 70)
        })
    End Sub

    Private Sub AjouterNote(texte As String, x As Integer, y As Integer)
        Me.Controls.Add(New Label() With {
            .Text      = texte,
            .Location  = New Point(x, y),
            .AutoSize  = True,
            .Font      = New Font("Segoe UI", 8, FontStyle.Italic),
            .ForeColor = Color.FromArgb(130, 140, 160)
        })
    End Sub

    Private Sub AjouterSeparateur(y As Integer)
        Me.Controls.Add(New Panel() With {
            .Location  = New Point(20, y),
            .Size      = New Size(516, 1),
            .BackColor = Color.FromArgb(200, 210, 230)
        })
    End Sub

End Class
