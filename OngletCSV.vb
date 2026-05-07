Imports System.Windows.Forms
Imports System.Drawing
Imports System.IO
Imports System.Diagnostics
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Microsoft.VisualBasic

''' <summary>
''' Onglet "Fichier CSV".
''' - Dossier et préfixe personnalisables.
''' - Format des valeurs numériques configurable (décimales fixes, scientifique, auto).
''' - Aperçu des colonnes à jour (toutes centrales + sorties + calculs + Notification).
''' </summary>
Public Class OngletCSV

    ' ─── Références externes ──────────────────────────────────────────────────
    Public Property Config       As ConfigManager
    Public Property GestionVoies As GestionVoies            ' compatibilité
    Public Property Gestionnaire As GestionnaireMultiCentrale
    Public Property GestCalculs  As GestionnaireCalculs

    ' ─── Contrôles ────────────────────────────────────────────────────────────
    Private _txtDossier       As New TextBox()
    Private _btnParcourir     As New Button()
    Private _btnOuvrirDossier As New Button()
    Private _txtPrefixe       As New TextBox()
    Private _lblNomAuto       As New Label()
    Private _lblNomComplet    As New Label()
    Private _btnSauver        As New Button()
    Private _dgvApercu        As New DataGridView()
    Private _timerNom         As New Timer() With {.Interval = 1000}
    Private _lblSepInfo       As New Label()
    Private _cmbFormat        As New ComboBox()
    Private _numDecimales     As New NumericUpDown()
    Private _cmbUniteDuree    As New ComboBox()
    Private _lblExemple       As New Label()

    ' ─── Constante séparateur ─────────────────────────────────────────────────
    Public Const SEPARATEUR As String = ";"

    ' ─── Format configurable ──────────────────────────────────────────────────
    Public Property FormatValeur  As String  = "F3"

    ''' <summary>Diviseur à appliquer aux secondes pour obtenir la durée dans l'unité choisie.</summary>
    Public ReadOnly Property DiviseurDuree As Double
        Get
            Select Case _cmbUniteDuree.SelectedIndex
                Case 1  : Return 60.0       ' minutes
                Case 2  : Return 3600.0     ' heures
                Case Else : Return 1.0      ' secondes (défaut)
            End Select
        End Get
    End Property

    ''' <summary>Libellé de l'unité de durée pour l'en-tête CSV.</summary>
    Public ReadOnly Property LibelleUniteDuree As String
        Get
            Select Case _cmbUniteDuree.SelectedIndex
                Case 1  : Return "Durée (min)"
                Case 2  : Return "Durée (h)"
                Case Else : Return "Durée (s)"
            End Select
        End Get
    End Property


    Public Property NbDecimales As Integer
        Get
            Return _nbDecimales
        End Get
        Set(value As Integer)
            _nbDecimales = Math.Max(0, Math.Min(10, value))
            _MettreAJourFormatDepuisControles()
        End Set
    End Property
    Private _nbDecimales As Integer = 3

    Public ReadOnly Property DossierCSV As String
        Get
            Return _txtDossier.Text.Trim()
        End Get
    End Property

    ''' <summary>Alias de compatibilité avec l'ancien code.</summary>
    Public ReadOnly Property NB_DECIMALES As Integer
        Get
            Return _nbDecimales
        End Get
    End Property

    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)

    ' ─── Construction du panneau ──────────────────────────────────────────────
    Public Function ConstruirePanel() As Panel
        Dim scroll As New Panel() With {.Dock = DockStyle.Fill, .AutoScroll = True}

        Dim pnl As New TableLayoutPanel() With {
            .Dock        = DockStyle.Top,
            .AutoSize    = True,
            .ColumnCount = 2,
            .Padding     = New Padding(14, 12, 14, 10)
        }
        pnl.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 200))
        pnl.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

        ' DOSSIER
        AjouterSep(pnl, "DOSSIER DE DESTINATION")
        _txtDossier.Font = New Font("Consolas", 9) : _txtDossier.Width = 380
        AddHandler _txtDossier.TextChanged, Sub(s, e) MettreAJourApercu()
        _btnParcourir.Text = "📁 Parcourir..." : _btnParcourir.FlatStyle = FlatStyle.Flat
        _btnParcourir.BackColor = Color.FromArgb(55, 60, 75) : _btnParcourir.ForeColor = Color.White
        _btnParcourir.Width = 110 : _btnParcourir.Height = 28
        _btnOuvrirDossier.Text = "🗂 Ouvrir" : _btnOuvrirDossier.FlatStyle = FlatStyle.Flat
        _btnOuvrirDossier.BackColor = Color.FromArgb(45, 50, 65) : _btnOuvrirDossier.ForeColor = Color.Silver
        _btnOuvrirDossier.Width = 75 : _btnOuvrirDossier.Height = 28
        Dim pnlDoss As New FlowLayoutPanel() With {.AutoSize = True}
        pnlDoss.Controls.AddRange({_txtDossier, _btnParcourir, _btnOuvrirDossier})
        AjouterLigne(pnl, "Dossier :", pnlDoss)

        ' NOM FICHIER
        AjouterSep(pnl, "NOM DU FICHIER")
        pnl.Controls.Add(New Label())
        pnl.Controls.Add(New Label() With {
            .Text = "Le nom est généré automatiquement à chaque démarrage d'acquisition.",
            .AutoSize = True, .ForeColor = Color.FromArgb(130, 140, 160),
            .Font = New Font("Segoe UI", 8.5, FontStyle.Italic)})
        _txtPrefixe.Font = New Font("Consolas", 10.5) : _txtPrefixe.Width = 200
        AddHandler _txtPrefixe.TextChanged, Sub(s, e) MettreAJourApercu()
        AjouterLigne(pnl, "Préfixe :", _txtPrefixe)
        Dim pnlEx As New FlowLayoutPanel() With {.AutoSize = True}
        For Each ex In {"Mesures_", "Essai_A_", "Charge_", "Decharge_", "Test_"}
            Dim b As New Button() With {.Text = ex, .FlatStyle = FlatStyle.Flat,
                .BackColor = Color.FromArgb(40, 45, 58), .ForeColor = Color.FromArgb(140, 180, 220),
                .Font = New Font("Consolas", 8.5), .Height = 22, .AutoSize = True,
                .Margin = New Padding(0, 0, 4, 0)}
            Dim cap = ex
            AddHandler b.Click, Sub(s, ev) _txtPrefixe.Text = cap
            pnlEx.Controls.Add(b)
        Next
        AjouterLigne(pnl, "Raccourcis :", pnlEx)
        _lblNomAuto.Font = New Font("Consolas", 10.5, FontStyle.Bold)
        _lblNomAuto.ForeColor = Color.FromArgb(80, 205, 145) : _lblNomAuto.AutoSize = True
        AjouterLigne(pnl, "Nom généré :", _lblNomAuto)
        _lblNomComplet.Font = New Font("Consolas", 8.5) : _lblNomComplet.ForeColor = Color.FromArgb(110, 120, 145)
        _lblNomComplet.AutoSize = True : _lblNomComplet.MaximumSize = New Size(560, 0)
        AjouterLigne(pnl, "Chemin complet :", _lblNomComplet)

        ' FORMAT DES VALEURS
        AjouterSep(pnl, "FORMAT DES VALEURS NUMÉRIQUES")
        _cmbFormat.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbFormat.Font = New Font("Consolas", 9.5) : _cmbFormat.Width = 240
        _cmbFormat.Items.AddRange({
            "Décimales fixes  (ex : 12.345)",
            "Scientifique     (ex : 1.235E+001)",
            "Général auto     (ex : 12.3 ou 1.23E+4)"})
        _cmbFormat.SelectedIndex = 0
        AddHandler _cmbFormat.SelectedIndexChanged, AddressOf Format_Changed
        AjouterLigne(pnl, "Format :", _cmbFormat)

        _numDecimales.Minimum = 0 : _numDecimales.Maximum = 10 : _numDecimales.Value = 3
        _numDecimales.Width = 60 : _numDecimales.Height = 24
        AddHandler _numDecimales.ValueChanged, AddressOf Format_Changed
        Dim pnlDec As New FlowLayoutPanel() With {.AutoSize = True}
        pnlDec.Controls.Add(_numDecimales)
        pnlDec.Controls.Add(New Label() With {.Text = " décimales", .AutoSize = True,
            .Margin = New Padding(4, 5, 0, 0), .ForeColor = Color.FromArgb(140, 150, 170),
            .Font = New Font("Segoe UI", 9)})
        AjouterLigne(pnl, "Nb décimales :", pnlDec)

        _lblExemple.AutoSize = True : _lblExemple.Font = New Font("Consolas", 9)
        _lblExemple.ForeColor = Color.FromArgb(200, 180, 80)
        AjouterLigne(pnl, "Exemple :", _lblExemple)

        ' Unité de la colonne durée
        _cmbUniteDuree.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbUniteDuree.Font = New Font("Consolas", 9.5) : _cmbUniteDuree.Width = 180
        _cmbUniteDuree.Items.AddRange({"Secondes (s)", "Minutes (min)", "Heures (h)"})
        _cmbUniteDuree.SelectedIndex = 0
        AddHandler _cmbUniteDuree.SelectedIndexChanged, Sub(s, e) MettreAJourApercu()
        AjouterLigne(pnl, "Unité durée :", _cmbUniteDuree)


        _dgvApercu.AllowUserToAddRows = False : _dgvApercu.AllowUserToDeleteRows = False
        _dgvApercu.ReadOnly = True : _dgvApercu.RowHeadersVisible = False
        _dgvApercu.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells
        _dgvApercu.BackgroundColor = Color.FromArgb(20, 23, 30)
        _dgvApercu.GridColor = Color.FromArgb(42, 48, 60)
        _dgvApercu.BorderStyle = BorderStyle.None
        _dgvApercu.Font = New Font("Consolas", 8.5)
        _dgvApercu.DefaultCellStyle.BackColor = Color.FromArgb(20, 23, 30)
        _dgvApercu.DefaultCellStyle.ForeColor = Color.FromArgb(170, 182, 200)
        _dgvApercu.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 38, 50)
        _dgvApercu.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(85, 140, 200)
        _dgvApercu.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        _dgvApercu.EnableHeadersVisualStyles = False
        _dgvApercu.Dock = DockStyle.Fill
        _dgvApercu.ScrollBars = ScrollBars.Both

        ' SAUVEGARDE
        AjouterSep(pnl, "SAUVEGARDE")
        _btnSauver.Text = "💾  Sauvegarder les paramètres CSV"
        _btnSauver.BackColor = Color.FromArgb(60, 65, 80) : _btnSauver.ForeColor = Color.White
        _btnSauver.FlatStyle = FlatStyle.Flat : _btnSauver.Width = 260 : _btnSauver.Height = 28
        _btnSauver.Font = New Font("Segoe UI", 8.5)
        AjouterLigne(pnl, "", _btnSauver)

        ' Conteneur principal : pnl (paramètres) en haut, dgvApercu en bas
        Dim outer As New Panel() With {.Dock = DockStyle.Fill}
        _lblSepInfo.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        _lblSepInfo.ForeColor = Color.FromArgb(80, 130, 190)
        _lblSepInfo.Dock = DockStyle.Top
        _lblSepInfo.Height = 24
        _lblSepInfo.Margin = New Padding(0)
        _lblSepInfo.Padding = New Padding(14, 6, 0, 0)
        Dim pnlApercu As New Panel() With {
            .Dock    = DockStyle.Fill,
            .Padding = New Padding(14, 0, 14, 8)
        }
        pnlApercu.Controls.Add(_dgvApercu)  ' Fill
        pnlApercu.Controls.Add(_lblSepInfo) ' Top
        outer.Controls.Add(pnlApercu)   ' Fill — en premier
        outer.Controls.Add(pnl)          ' Top — en second (WinForms : Top s'ancre avant Fill)
        scroll.Controls.Add(outer)

        AddHandler _btnParcourir.Click,     AddressOf Parcourir_Click
        AddHandler _btnOuvrirDossier.Click, AddressOf OuvrirDossier_Click
        AddHandler _btnSauver.Click,        AddressOf Sauver_Click
        AddHandler _timerNom.Tick,          Sub(s, e) MettreAJourApercu()
        _timerNom.Start()
        _MettreAJourFormatDepuisControles()
        Return scroll
    End Function

    ' ─── Config ───────────────────────────────────────────────────────────────
    Public Sub ChargerDepuisConfig()
        _txtDossier.Text = Config.Get_(ConfigManager.SEC_CSV, "Dossier",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Thermopilot"))
        _txtPrefixe.Text = Config.Get_(ConfigManager.SEC_CSV, "Prefixe", "Mesures_")
        Dim idx = Config.GetInt(ConfigManager.SEC_CSV, "FormatIndex", 0)
        If _cmbFormat.Items.Count > 0 Then
            _cmbFormat.SelectedIndex = Math.Max(0, Math.Min(_cmbFormat.Items.Count - 1, idx))
        End If
        _numDecimales.Value = Config.GetInt(ConfigManager.SEC_CSV, "NbDecimales", 3)
        _cmbUniteDuree.SelectedIndex = Config.GetInt(ConfigManager.SEC_CSV, "UniteDuree", 0)
        _MettreAJourFormatDepuisControles()
        MettreAJourApercu()
    End Sub

    ' ─── Format ───────────────────────────────────────────────────────────────
    Private Sub Format_Changed(sender As Object, e As EventArgs)
        _MettreAJourFormatDepuisControles()
        MettreAJourApercu()
    End Sub

    Private Sub _MettreAJourFormatDepuisControles()
        Dim dec = CInt(_numDecimales.Value)
        _nbDecimales = dec
        Dim idx = If(_cmbFormat.SelectedIndex >= 0, _cmbFormat.SelectedIndex, 0)
        _numDecimales.Enabled = (idx <> 2)
        Select Case idx
            Case 0 : FormatValeur = "F" & dec.ToString()
            Case 1 : FormatValeur = "E" & dec.ToString()
            Case 2 : FormatValeur = "G"
            Case Else : FormatValeur = "F3"
        End Select
        Try
            Dim ex As Double = 12.3456789
            _lblExemple.Text = ex.ToString(FormatValeur,
                System.Globalization.CultureInfo.InvariantCulture) & "   (test : 12.3456789)"
        Catch
            _lblExemple.Text = "Format invalide"
        End Try
        If _lblSepInfo IsNot Nothing Then
            _lblSepInfo.Text = String.Format(
                "APERÇU DES COLONNES CSV  (séparateur '{0}' · format {1})",
                SEPARATEUR, FormatValeur)
        End If
    End Sub

    ' ─── Aperçu ───────────────────────────────────────────────────────────────
    Public Sub MettreAJourApercu()
        Dim nomAuto = NomFichierAuto()
        _lblNomAuto.Text    = nomAuto
        _lblNomComplet.Text = Path.Combine(_txtDossier.Text.Trim(), nomAuto)

        _dgvApercu.Columns.Clear()
        _dgvApercu.Rows.Clear()
        _dgvApercu.Columns.Add("C0", "Horodatage")
        _dgvApercu.Columns.Add("C1", LibelleUniteDuree)
        Dim vals As New List(Of String)
        vals.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        vals.Add("0")   'durée exemple (format G — pas d'arrondi)

        Dim aCentrale = Gestionnaire IsNot Nothing AndAlso
                        Gestionnaire.Centrales.Any(Function(c) c.Voies.Voies.Any(Function(v) v.Active))

        If aCentrale Then
            For Each c In Gestionnaire.Centrales
                For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                    _dgvApercu.Columns.Add("", String.Format("{0}_{1} ({2})", c.NomAffiche, v.Nom, v.Unite))
                    vals.Add(If(v.EnErreur OrElse Double.IsNaN(v.Valeur), "ERR", FormaterValeur(v.Valeur)))
                Next
                For Each s In c.Voies.SortiesActives()
                    Select Case s.Mode
                        Case SortieAnalogique.ModePilotage.Analogique,
                             SortieAnalogique.ModePilotage.AnalogiqueFull
                            _dgvApercu.Columns.Add("", String.Format("{0}_{1} (V)", c.NomAffiche, s.Nom))
                            vals.Add(FormaterValeur(s.TensionV))
                        Case Else
                            _dgvApercu.Columns.Add("", String.Format("{0}_{1} (ON/OFF)", c.NomAffiche, s.Nom))
                            vals.Add(If(s.EstOn, "1", "0"))
                    End Select
                Next
            Next
        Else
            For Each ex In {("C1_T_101", "°C", 62.345), ("C1_T_102", "°C", 58.12), ("C1_Débit", "L/h", 42.7)}
                _dgvApercu.Columns.Add("", String.Format("{0} ({1})", ex.Item1, ex.Item2))
                vals.Add(FormaterValeur(ex.Item3))
            Next
        End If

        If GestCalculs IsNot Nothing Then
            For Each vc In GestCalculs.Voies.Where(Function(v) v.Active)
                _dgvApercu.Columns.Add("", String.Format("[Calcul] {0} ({1})", vc.Nom, vc.Unite))
                vals.Add(If(vc.EnErreur OrElse Double.IsNaN(vc.Valeur), "ERR", FormaterValeur(vc.Valeur)))
            Next
        End If

        ' Colonne Notification
        _dgvApercu.Columns.Add("CNotif", "Notification")
        vals.Add("")

        _dgvApercu.Rows.Add(vals.ToArray())

        ' Style colonne Notification
        With _dgvApercu.Columns("CNotif")
            .DefaultCellStyle.ForeColor = Color.FromArgb(200, 160, 60)
            .DefaultCellStyle.BackColor = Color.FromArgb(28, 26, 20)
            .HeaderCell.Style.ForeColor = Color.FromArgb(200, 160, 60)
        End With
        For i As Integer = 0 To _dgvApercu.Columns.Count - 2
            _dgvApercu.Columns(i).HeaderCell.Style.BackColor = Color.FromArgb(32, 38, 50)
        Next
    End Sub

    Public Function FormaterValeur(valeur As Double) As String
        Try
            Return valeur.ToString(FormatValeur,
                System.Globalization.CultureInfo.InvariantCulture)
        Catch
            Return valeur.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
        End Try
    End Function

    ' ─── Nom fichier ──────────────────────────────────────────────────────────
    Public Function NomFichierAuto() As String
        Dim ts = DateTime.Now.ToString("yyyyMMdd-HHmmss")
        Return NettoierPrefixe(_txtPrefixe.Text) & ts & ".csv"
    End Function

    Public Function CheminFige() As String
        Return Path.Combine(_txtDossier.Text.Trim(), NomFichierAuto())
    End Function

    ''' <summary>Retourne le chemin CSV avec un horodatage partagé (même que graphique et rapport).</summary>
    Public Function CheminFigeAvecHorodatage(horodatage As String) As String
        Dim nomFichier = NettoierPrefixe(_txtPrefixe.Text) & horodatage & ".csv"
        Return Path.Combine(_txtDossier.Text.Trim(), nomFichier)
    End Function

    Private Function NettoierPrefixe(prefixe As String) As String
        Return New String(prefixe.Trim().Where(Function(c) Not IO.Path.GetInvalidFileNameChars().Contains(c)).ToArray())
    End Function

    ' ─── Gestionnaires ────────────────────────────────────────────────────────
    Private Sub Parcourir_Click(sender As Object, e As EventArgs)
        Using dlg As New FolderBrowserDialog() With {
            .Description = "Choisir le dossier de destination", .SelectedPath = _txtDossier.Text}
            If dlg.ShowDialog() = DialogResult.OK Then
                _txtDossier.Text = dlg.SelectedPath
                MettreAJourApercu()
            End If
        End Using
    End Sub

    Private Sub OuvrirDossier_Click(sender As Object, e As EventArgs)
        Try
            Dim d = _txtDossier.Text.Trim()
            If Not Directory.Exists(d) Then Directory.CreateDirectory(d)
            Process.Start("explorer.exe", d)
        Catch ex As Exception
            MsgBox("Impossible d'ouvrir le dossier : " & ex.Message, MsgBoxStyle.Exclamation)
        End Try
    End Sub

    Private Sub Sauver_Click(sender As Object, e As EventArgs)
        Config.Set_(ConfigManager.SEC_CSV, "Dossier",     _txtDossier.Text.Trim())
        Config.Set_(ConfigManager.SEC_CSV, "Prefixe",     NettoierPrefixe(_txtPrefixe.Text))
        Config.Set_(ConfigManager.SEC_CSV, "FormatIndex",  _cmbFormat.SelectedIndex)
        Config.Set_(ConfigManager.SEC_CSV, "NbDecimales",  CInt(_numDecimales.Value))
        Config.Set_(ConfigManager.SEC_CSV, "UniteDuree",   _cmbUniteDuree.SelectedIndex)
        Try
            Config.Sauvegarder()
            RaiseEvent StatutChange(Me, "Paramètres CSV sauvegardés.", False)
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical)
        End Try
    End Sub

    ' ─── Layout helpers ───────────────────────────────────────────────────────
    Private Sub AjouterSep(pnl As TableLayoutPanel, titre As String)
        Dim lbl As New Label() With {
            .Text = titre, .Font = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(80, 130, 190), .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 14, 0, 4)}
        pnl.Controls.Add(lbl) : pnl.SetColumnSpan(lbl, 2)
    End Sub

    Private Sub AjouterLigne(pnl As TableLayoutPanel, etiquette As String, ctrl As Control)
        pnl.Controls.Add(New Label() With {
            .Text = etiquette, .AutoSize = True,
            .ForeColor = Color.FromArgb(140, 150, 170),
            .Font = New Font("Segoe UI", 9),
            .Margin = New Padding(0, 6, 8, 0),
            .TextAlign = ContentAlignment.MiddleRight})
        ctrl.Margin = New Padding(0, 2, 0, 2)
        pnl.Controls.Add(ctrl)
    End Sub

End Class
