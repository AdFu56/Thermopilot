Imports System.Windows.Forms
Imports System.Drawing
Imports System.IO
Imports System.Diagnostics
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Microsoft.VisualBasic

'''<summary>
''' Onglet "Résultats" — ouvre et trace des fichiers CSV Thermopilot.
''' Plusieurs sous-onglets indépendants avec graphique OxyPlot, cases à cocher,
''' zone calculs utilisateur, tableau, statistiques.
''' </summary>
Public Class OngletVisuCSV

    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)

    Private _tabResultats    As New TabControl()
    Private _btnNouvelOnglet As New Button()
    Private _btnFermerOnglet As New Button()
    Private _compteurOnglet  As Integer = 0
    Public Property Config         As ConfigManager = Nothing
    Public Property DossierDefaut  As String = ""
    Private _panneaux As New List(Of PanneauVisuCSV)()

    Public Function ConstruirePanel() As Control
        Dim conteneur As New Panel() With {.Dock = DockStyle.Fill}

        Dim tbHaut As New FlowLayoutPanel() With {
            .Dock = DockStyle.Top, .Height = 38,
            .FlowDirection = FlowDirection.LeftToRight,
            .Padding = New Padding(6, 4, 0, 0),
            .BackColor = Color.FromArgb(36, 40, 52)}

        _btnNouvelOnglet.Text      = "➕ Nouvel onglet"
        _btnNouvelOnglet.BackColor = Color.FromArgb(40, 110, 175)
        _btnNouvelOnglet.ForeColor = Color.White
        _btnNouvelOnglet.FlatStyle = FlatStyle.Flat
        _btnNouvelOnglet.Height    = 28
        _btnNouvelOnglet.Width     = 140
        _btnNouvelOnglet.Margin    = New Padding(0, 2, 8, 0)

        _btnFermerOnglet.Text      = "✖ Fermer onglet"
        _btnFermerOnglet.BackColor = Color.FromArgb(140, 40, 40)
        _btnFermerOnglet.ForeColor = Color.White
        _btnFermerOnglet.FlatStyle = FlatStyle.Flat
        _btnFermerOnglet.Height    = 28
        _btnFermerOnglet.Width     = 130
        _btnFermerOnglet.Margin    = New Padding(0, 2, 0, 0)

        tbHaut.Controls.AddRange({_btnNouvelOnglet, _btnFermerOnglet})
        _tabResultats.Dock = DockStyle.Fill
        _tabResultats.Font = New Font("Segoe UI", 9)

        conteneur.Controls.Add(_tabResultats)
        conteneur.Controls.Add(tbHaut)

        AddHandler _btnNouvelOnglet.Click, AddressOf AjouterOnglet
        AddHandler _btnFermerOnglet.Click, AddressOf FermerOngletCourant
        AddHandler _tabResultats.DoubleClick, AddressOf OnTabDoubleClick

        AjouterOnglet(Nothing, EventArgs.Empty)
        Return conteneur
    End Function

    ' ─── Persistance ─────────────────────────────────────────────────────────

    Public Sub SauverEtat()
        If Config Is Nothing Then Return
        Dim nb = _tabResultats.TabPages.Count
        Config.Set_("Resultats", "NbOnglets", nb)
        For i As Integer = 0 To nb - 1
            Dim page = _tabResultats.TabPages(i)
            Dim pnl  = If(i < _panneaux.Count, _panneaux(i), Nothing)
            Config.Set_("Resultats", "Onglet" & i & "_Titre", page.Text)
            Config.Set_("Resultats", "Onglet" & i & "_Fichier",
                If(pnl IsNot Nothing, pnl.CheminCourant, ""))
            Config.Set_("Resultats", "Onglet" & i & "_CalcsMasques",
                If(pnl IsNot Nothing, pnl.CalcsMasques, False))
            Config.Set_("Resultats", "Onglet" & i & "_TableauMasque",
                If(pnl IsNot Nothing, pnl.TableauMasque, False))
            If pnl IsNot Nothing Then pnl.SauverStyles(Config, "Resultats_Onglet" & i)
        Next
    End Sub

    Public Sub ChargerEtat()
        If Config Is Nothing Then Return
        Dim nb = Config.GetInt("Resultats", "NbOnglets", 0)
        If nb = 0 Then Return
        ' Vider les onglets et la liste des panneaux
        _tabResultats.TabPages.Clear()
        _panneaux.Clear()
        For i As Integer = 0 To nb - 1
            _compteurOnglet += 1
            Dim titre   = Config.Get_("Resultats", "Onglet" & i & "_Titre",
                                      "Résultats " & _compteurOnglet)
            Dim fichier = Config.Get_("Resultats", "Onglet" & i & "_Fichier", "")
            Dim calcsMasques  = Config.GetBool("Resultats", "Onglet" & i & "_CalcsMasques", False)
            Dim tableauMasque = Config.GetBool("Resultats", "Onglet" & i & "_TableauMasque", False)
            Dim page As New TabPage(titre)
            Dim panneau As New PanneauVisuCSV()
            panneau.DossierDefaut = DossierDefaut
            AddHandler panneau.StatutChange,
                Sub(s As Object, msg As String, err As Boolean)
                    RaiseEvent StatutChange(Me, msg, err)
                End Sub
            page.Controls.Add(panneau.ConstruirePanel())
            _panneaux.Add(panneau)
            _tabResultats.TabPages.Add(page)
            ' Appliquer l'état masqué
            If calcsMasques  Then panneau.MasquerCalcs()
            If tableauMasque Then panneau.MasquerTableau()
            panneau.ChargerStyles(Config, "Resultats_Onglet" & i)
            ' Assigner Config et SectionConfig au panneau graphique AVANT ChargerFichier
            ' pour que DefinirSeries charge les styles par série dans la bonne section
            panneau.SetConfigGraphique(Config, "Resultats_Onglet" & i)
            ' Recharger le fichier CSV si existant
            If fichier <> "" AndAlso IO.File.Exists(fichier) Then
                panneau.ChargerFichier(fichier)
            End If
        Next
        If _tabResultats.TabPages.Count = 0 Then
            AjouterOnglet(Nothing, EventArgs.Empty)
        End If
    End Sub

    Private Sub AjouterOnglet(sender As Object, e As EventArgs)
        _compteurOnglet += 1
        Dim page As New TabPage("Résultats " & _compteurOnglet)
        Dim panneau As New PanneauVisuCSV()
        panneau.DossierDefaut = DossierDefaut
        AddHandler panneau.StatutChange,
            Sub(s As Object, msg As String, err As Boolean)
                RaiseEvent StatutChange(Me, msg, err)
            End Sub
        page.Controls.Add(panneau.ConstruirePanel())
        _panneaux.Add(panneau)
        _tabResultats.TabPages.Add(page)
        _tabResultats.SelectedTab = page
    End Sub

    Private Sub OnTabDoubleClick(sender As Object, e As EventArgs)
        Dim tab = _tabResultats.SelectedTab
        If tab Is Nothing Then Return
        Dim nom = InputBox("Nouveau nom pour cet onglet :", "Renommer", tab.Text)
        If nom <> "" Then tab.Text = nom
    End Sub

    Private Sub FermerOngletCourant(sender As Object, e As EventArgs)
        If _tabResultats.TabPages.Count <= 1 Then Return
        Dim idx  = _tabResultats.SelectedIndex
        Dim page = _tabResultats.SelectedTab
        _tabResultats.TabPages.Remove(page)
        If idx >= 0 AndAlso idx < _panneaux.Count Then _panneaux.RemoveAt(idx)
        page.Dispose()
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  PANNEAU VISU CSV — un sous-onglet complet
' ═══════════════════════════════════════════════════════════════════════════════

Public Class PanneauVisuCSV

    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)

    Private _txtFichier     As New TextBox()
    Private _btnOuvrir      As New Button()
    Private _btnDossier     As New Button()
    Private _btnRecharger   As New Button()
    Private _lblInfo        As New Label()
    Private _panelGraphe    As New PanelGraphique()
    Private _lstSeries      As New CheckedListBox()
    Private _btnTout        As New Button()
    Private _btnAucun       As New Button()
    Private _lblStats       As New Label()
    Private _dgvDonnees     As New DataGridView()
    Private _txtCalcNom     As New TextBox()
    Private _txtCalcUnite   As New TextBox()
    Private _txtCalcExpr    As New TextBox()
    Private _numCalcMoy     As New NumericUpDown()
    Private _btnCalcAjouter As New Button()
    Private _lstCalcs       As New ListBox()
    Private _btnCalcSuppr    As New Button()
    Private _lstCalcsVisu    As New CheckedListBox()  ' variables calculées dans onglet Mesures
    Private _cmbColonnes     As New ComboBox()     ' liste des colonnes CSV
    Private _btnInsererCalc  As New Button()       ' insérer {Cx} dans expression
    Private _btnTesterCalc   As New Button()       ' tester l'expression
    Private _lblResultatCalc As New Label()        ' résultat du test
    Private _indexEditeCalc   As Integer = -1
    Private _chargementCalc   As Boolean = False
    Private _numFenetre          As New NumericUpDown()
    Private _cmbUniteFen         As New ComboBox()
    Private _btnMasquerCalcs     As New Button()
    Private _btnMasquerTableau   As New Button()
    Private _btnModeGraphique    As New Button()
    Private _splitGauche         As SplitContainer = Nothing
    Private _splitDroite         As SplitContainer = Nothing
    Private _tblCalc             As TableLayoutPanel = Nothing  ' référence pour toggle aide

    Private _colonnes         As New List(Of String)
    Private _indicesColonnes  As New List(Of Integer) ' correspondance lstSeries(k) → _colonnes(idx)
    Private _lignes         As New List(Of String())
    Private _horodatages    As New List(Of DateTime)
    Private _historiqueVisu As New HistoriqueMultiCentrale(50000)
    Private _seriesVisu     As New List(Of PanelGraphique.SerieGraphique)
    Private _gestCalculs    As New GestionnaireCalculs()
    Private _cheminCourant  As String = ""
    Private _separateur     As Char = ";"c
    Private _dernierDossier As String = ""

    ' ─── Propriétés publiques ────────────────────────────────────────────────

    Public Property DossierDefaut As String = ""  ' alimenté depuis OngletCSV

    Public ReadOnly Property CheminCourant As String
        Get
            Return _cheminCourant
        End Get
    End Property

    Public ReadOnly Property CalcsMasques As Boolean
        Get
            ' Dans la nouvelle disposition, "masquer calculs" = panneau gauche collapsed
            Return If(_splitGauche IsNot Nothing, _splitGauche.Panel2Collapsed, False)
        End Get
    End Property

    Public ReadOnly Property TableauMasque As Boolean
        Get
            Return If(_splitDroite IsNot Nothing, _splitDroite.Panel2Collapsed, False)
        End Get
    End Property

    Public Sub MasquerCalcs()
        If _splitGauche IsNot Nothing Then
            _splitGauche.Panel2Collapsed = True
            _btnMasquerCalcs.Text = "🧮 Afficher calculs"
        End If
    End Sub

    Public Sub MasquerTableau()
        If _splitDroite IsNot Nothing Then
            _splitDroite.Panel2Collapsed = True
            _btnMasquerTableau.Text = "⊟ Afficher tableau"
        End If
    End Sub

    Public Sub SauverStyles(cfg As ConfigManager, section As String)
        If cfg Is Nothing Then Return
        _panelGraphe.Styles.SauverDansConfig(cfg, section)
    End Sub

    Public Sub ChargerStyles(cfg As ConfigManager, section As String)
        If cfg Is Nothing Then Return
        _panelGraphe.Styles.ChargerDepuisConfig(cfg, section)
    End Sub

    ''' <summary>
    ''' Configure le ConfigManager et la section du panneau graphique.
    ''' À appeler AVANT ChargerFichier pour que DefinirSeries charge les styles par série.
    ''' </summary>
    Public Sub SetConfigGraphique(cfg As ConfigManager, section As String)
        If cfg Is Nothing Then Return
        _panelGraphe.SectionConfig = section
        _panelGraphe.Config        = cfg
    End Sub

    Public Function ConstruirePanel() As Control
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}

        ' Barre d'outils
        Dim tb As New FlowLayoutPanel() With {
            .Dock = DockStyle.Top, .Height = 38,
            .FlowDirection = FlowDirection.LeftToRight,
            .Padding = New Padding(4, 4, 0, 0)}

        _btnOuvrir.Text      = "📂 Ouvrir CSV…"
        _btnOuvrir.BackColor = Color.FromArgb(40, 110, 175)
        _btnOuvrir.ForeColor = Color.White : _btnOuvrir.FlatStyle = FlatStyle.Flat
        _btnOuvrir.Height    = 26 : _btnOuvrir.Width = 130
        _btnOuvrir.Margin    = New Padding(0, 2, 4, 0)

        _btnDossier.Text      = "🗂 Dossier"
        _btnDossier.BackColor = Color.FromArgb(55, 60, 75)
        _btnDossier.ForeColor = Color.White : _btnDossier.FlatStyle = FlatStyle.Flat
        _btnDossier.Height    = 26 : _btnDossier.Width = 80
        _btnDossier.Margin    = New Padding(0, 2, 4, 0)

        _btnRecharger.Text      = "Recharger"
        _btnRecharger.BackColor = Color.FromArgb(55, 60, 75)
        _btnRecharger.ForeColor = Color.White : _btnRecharger.FlatStyle = FlatStyle.Flat
        _btnRecharger.Height    = 26 : _btnRecharger.Width = 90
        _btnRecharger.Margin    = New Padding(0, 2, 8, 0)
        _btnRecharger.Enabled   = False

        _txtFichier.ReadOnly  = True : _txtFichier.Width = 340
        _txtFichier.Font      = New Font("Consolas", 8.5)
        _txtFichier.ForeColor = Color.FromArgb(60, 80, 120)
        _txtFichier.Margin    = New Padding(0, 3, 8, 0)

        Dim lblFen As New Label() With {
            .Text = "Fenêtre :", .Width = 60, .Height = 24,
            .Font = New Font("Segoe UI", 8.5), .ForeColor = Color.Gray,
            .Margin = New Padding(0, 5, 2, 0)}
        _numFenetre.Minimum = 0 : _numFenetre.Maximum = 9999
        _numFenetre.Value   = 0 : _numFenetre.Width = 55 : _numFenetre.Height = 24
        _numFenetre.Margin  = New Padding(0, 3, 2, 0)
        _cmbUniteFen.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbUniteFen.Width = 58 : _cmbUniteFen.Height = 24
        _cmbUniteFen.Margin = New Padding(0, 3, 0, 0)
        _cmbUniteFen.Items.AddRange({"[s]", "[min]", "[h]"})
        _cmbUniteFen.SelectedIndex = 1

        _lblInfo.AutoSize = True : _lblInfo.ForeColor = Color.Gray
        _lblInfo.Font = New Font("Segoe UI", 8, FontStyle.Italic)
        _lblInfo.Margin = New Padding(12, 6, 0, 0)

        _btnMasquerCalcs.Text      = "🧮 Masquer calculs"
        _btnMasquerCalcs.BackColor = Color.FromArgb(40, 65, 100)
        _btnMasquerCalcs.ForeColor = Color.White
        _btnMasquerCalcs.FlatStyle = FlatStyle.Flat
        _btnMasquerCalcs.Height    = 26 : _btnMasquerCalcs.Width = 145
        _btnMasquerCalcs.Margin    = New Padding(8, 2, 4, 0)
        _btnModeGraphique.Text      = "📊 Histogramme"
        _btnModeGraphique.BackColor = Color.FromArgb(60, 50, 90)
        _btnModeGraphique.ForeColor = Color.White
        _btnModeGraphique.FlatStyle = FlatStyle.Flat
        _btnModeGraphique.Height    = 26 : _btnModeGraphique.Width = 115
        _btnModeGraphique.Margin    = New Padding(8, 2, 4, 0)
        _btnMasquerTableau.Text      = "⊞ Masquer tableau"
        _btnMasquerTableau.BackColor = Color.FromArgb(55, 60, 80)
        _btnMasquerTableau.ForeColor = Color.White
        _btnMasquerTableau.FlatStyle = FlatStyle.Flat
        _btnMasquerTableau.Height    = 26 : _btnMasquerTableau.Width = 140
        _btnMasquerTableau.Margin    = New Padding(0, 2, 0, 0)

        tb.Controls.AddRange({_btnOuvrir, _btnDossier, _btnRecharger,
                               _txtFichier,
                               _btnMasquerCalcs, _btnMasquerTableau,
                               _btnModeGraphique, _lblInfo})

        ' Corps principal : gauche (TabControl Voies/Calculs) | droite (graphique+tableau)
        Dim splitMain As New SplitContainer() With {.Dock = DockStyle.Fill}
        AddHandler splitMain.HandleCreated,
            Sub(sc, ev)
                splitMain.BeginInvoke(New Action(Sub()
                    Try
                        splitMain.Panel1MinSize = 260
                        If 300 >= splitMain.Panel1MinSize AndAlso
                           300 <= splitMain.Width - 10 Then
                            splitMain.SplitterDistance = 300
                        End If
                    Catch
                    End Try
                End Sub))
            End Sub

        ' ── Panneau gauche : TabControl Voies / Calculs ──
        Dim tabGauche As New TabControl() With {
            .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 8.5)}
        Dim tabPageVoies  As New TabPage("📈 Mesures")
        Dim tabPageCalcs  As New TabPage("🧮 Calculs")

        ' ── Onglet Voies : SplitContainer Mesures (haut) / Variables calculées (bas) ──
        Dim splitVoies As New SplitContainer() With {
            .Dock        = DockStyle.Fill,
            .Orientation = Orientation.Horizontal}
        AddHandler splitVoies.HandleCreated,
            Sub(sc, ev)
                splitVoies.BeginInvoke(New Action(Sub()
                    Try
                        splitVoies.Panel1MinSize = 60
                        splitVoies.Panel2MinSize = 40
                        Dim d = CInt(splitVoies.Height * 0.65)
                        If d >= splitVoies.Panel1MinSize AndAlso
                           d <= splitVoies.Height - splitVoies.Panel2MinSize Then
                            splitVoies.SplitterDistance = d
                        End If
                    Catch
                    End Try
                End Sub))
            End Sub

        ' Panneau haut : liste Mesures
        Dim pnlMesures As New Panel() With {.Dock = DockStyle.Fill}
        Dim lblSeries As New Label() With {
            .Text = "MESURES", .Dock = DockStyle.Top, .Height = 20,
            .Font = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(80, 130, 190), .Padding = New Padding(4, 2, 0, 0)}
        _lstSeries.Dock = DockStyle.Fill
        _lstSeries.Font = New Font("Segoe UI", 8.5) : _lstSeries.CheckOnClick = True
        Dim pnlBtns As New FlowLayoutPanel() With {
            .Dock = DockStyle.Bottom, .Height = 28, .Padding = New Padding(2, 2, 0, 0)}
        _btnTout.Text = "Tout"   : _btnTout.Width  = 50 : _btnTout.Height  = 22
        _btnAucun.Text = "Aucun" : _btnAucun.Width = 55 : _btnAucun.Height = 22
        _btnTout.FlatStyle = FlatStyle.Flat : _btnAucun.FlatStyle = FlatStyle.Flat
        pnlBtns.Controls.AddRange({_btnTout, _btnAucun})
        pnlMesures.Controls.Add(_lstSeries)
        pnlMesures.Controls.Add(pnlBtns)
        pnlMesures.Controls.Add(lblSeries)
        splitVoies.Panel1.Controls.Add(pnlMesures)

        ' Panneau bas : Variables calculées
        Dim pnlCalcsVisu As New Panel() With {.Dock = DockStyle.Fill}
        Dim lblCalcsVisu As New Label() With {
            .Text = "VARIABLES CALCULÉES", .Dock = DockStyle.Top, .Height = 20,
            .Font = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(80, 130, 190), .BackColor = Color.FromArgb(240, 244, 252),
            .Padding = New Padding(4, 2, 0, 0)}
        _lstCalcsVisu.Dock = DockStyle.Fill
        _lstCalcsVisu.Font = New Font("Segoe UI", 8.5) : _lstCalcsVisu.CheckOnClick = True
        pnlCalcsVisu.Controls.Add(_lstCalcsVisu)
        pnlCalcsVisu.Controls.Add(lblCalcsVisu)
        splitVoies.Panel2.Controls.Add(pnlCalcsVisu)

        tabPageVoies.Controls.Add(splitVoies)

        ' ── Onglet Calculs : SplitContainer liste (haut) / éditeur scrollable (bas) ──
        Dim pnlCalcs As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.White}
        Dim splitCalcs As New SplitContainer() With {
            .Dock = DockStyle.Fill, .Orientation = Orientation.Horizontal}
        AddHandler splitCalcs.HandleCreated,
            Sub(sc, ev)
                splitCalcs.BeginInvoke(New Action(Sub()
                    Try
                        splitCalcs.Panel1MinSize = 90
                        splitCalcs.Panel2MinSize = 180
                        Dim d = CInt(splitCalcs.Height * 0.30)
                        If d < splitCalcs.Panel1MinSize Then d = splitCalcs.Panel1MinSize
                        If d <= splitCalcs.Height - splitCalcs.Panel2MinSize Then
                            splitCalcs.SplitterDistance = d
                        End If
                    Catch
                    End Try
                End Sub))
            End Sub

        ' ── Panel haut : liste + bouton Supprimer ──
        _lstCalcs.Dock = DockStyle.Fill
        _lstCalcs.Font = New Font("Segoe UI", 8.5)
        Dim flSuppr As New FlowLayoutPanel() With {
            .Dock = DockStyle.Bottom, .Height = 28, .Padding = New Padding(2, 2, 0, 0)}
        _btnCalcSuppr.Text = "✕ Supprimer" : _btnCalcSuppr.Height = 24 : _btnCalcSuppr.Width = 95
        _btnCalcSuppr.FlatStyle = FlatStyle.Flat
        _btnCalcSuppr.BackColor = Color.FromArgb(140, 40, 40) : _btnCalcSuppr.ForeColor = Color.White
        flSuppr.Controls.Add(_btnCalcSuppr)
        splitCalcs.Panel1.Controls.Add(_lstCalcs)
        splitCalcs.Panel1.Controls.Add(flSuppr)

        ' ── Panel bas : éditeur avec scroll ──
        Dim scrollEditeur As New Panel() With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = Color.White}

        Dim pnlEditeur As New Panel() With {
            .BackColor = Color.White, .Padding = New Padding(4),
            .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink}

        ' Ligne 0 : Nom / Unité / Moy
        Dim pnlNomUnite As New FlowLayoutPanel() With {
            .AutoSize = True, .BackColor = Color.White, .Margin = New Padding(0, 2, 0, 2)}
        _txtCalcNom.Width = 85 : _txtCalcNom.Font = New Font("Segoe UI", 8.5) : _txtCalcNom.Margin = New Padding(0, 3, 4, 0)
        _txtCalcUnite.Width = 48 : _txtCalcUnite.Font = New Font("Segoe UI", 8.5) : _txtCalcUnite.Margin = New Padding(0, 3, 4, 0)
        _numCalcMoy.Minimum = 1 : _numCalcMoy.Maximum = 200 : _numCalcMoy.Value = 1
        _numCalcMoy.Width = 42 : _numCalcMoy.Margin = New Padding(0, 3, 0, 0)
        pnlNomUnite.Controls.AddRange({
            New Label() With {.Text="Nom :",.AutoSize=True,.Margin=New Padding(0,6,2,0),.ForeColor=Color.FromArgb(40,60,100)},
            _txtCalcNom,
            New Label() With {.Text="Unité :",.AutoSize=True,.Margin=New Padding(0,6,2,0),.ForeColor=Color.FromArgb(40,60,100)},
            _txtCalcUnite,
            New Label() With {.Text="Moy.:",.AutoSize=True,.Margin=New Padding(0,6,2,0),.ForeColor=Color.FromArgb(40,60,100)},
            _numCalcMoy})

        ' Ligne 1 : Insérer colonne
        Dim pnlInserer As New FlowLayoutPanel() With {.AutoSize = True, .BackColor = Color.White, .Margin = New Padding(0, 2, 0, 2)}
        _cmbColonnes.DropDownStyle = ComboBoxStyle.DropDownList
        _cmbColonnes.Width = 150 : _cmbColonnes.Font = New Font("Consolas", 7.5) : _cmbColonnes.Margin = New Padding(0, 2, 4, 0)
        _btnInsererCalc.Text = "← Insérer" : _btnInsererCalc.Height = 24 : _btnInsererCalc.Width = 72
        _btnInsererCalc.FlatStyle = FlatStyle.Flat
        _btnInsererCalc.BackColor = Color.FromArgb(70, 80, 110) : _btnInsererCalc.ForeColor = Color.White
        _btnInsererCalc.Margin = New Padding(0, 2, 0, 0)
        pnlInserer.Controls.AddRange({
            New Label() With {.Text="Insérer :",.AutoSize=True,.Margin=New Padding(0,6,3,0),.ForeColor=Color.FromArgb(40,60,100)},
            _cmbColonnes, _btnInsererCalc})

        ' Label Expression
        Dim lblExprCalc As New Label() With {
            .Text = "Expression mathématique :", .AutoSize = True,
            .Font = New Font("Segoe UI", 8, FontStyle.Bold), .ForeColor = Color.FromArgb(40, 60, 100),
            .Margin = New Padding(0, 4, 0, 1)}

        ' Zone expression
        _txtCalcExpr.Multiline = True : _txtCalcExpr.Height = 55
        _txtCalcExpr.ScrollBars = ScrollBars.Vertical
        _txtCalcExpr.Font = New Font("Consolas", 9)
        _txtCalcExpr.BackColor = Color.FromArgb(245, 248, 255) : _txtCalcExpr.ForeColor = Color.FromArgb(20, 60, 140)
        _txtCalcExpr.Width = 240 : _txtCalcExpr.Margin = New Padding(0, 0, 0, 2)

        ' Résultat test
        _lblResultatCalc.AutoSize = True
        _lblResultatCalc.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        _lblResultatCalc.ForeColor = Color.FromArgb(0, 140, 70) : _lblResultatCalc.Text = ""
        _lblResultatCalc.Margin = New Padding(0, 0, 0, 2)

        ' Boutons Calculer + Tester — même hauteur, même Padding vertical, alignés
        Dim pnlBtnsCalc As New FlowLayoutPanel() With {
            .AutoSize = True, .BackColor = Color.White,
            .Margin = New Padding(0, 2, 0, 4), .WrapContents = False}
        _btnCalcAjouter.Text = "➕ Calculer" : _btnCalcAjouter.Height = 26 : _btnCalcAjouter.Width = 92
        _btnCalcAjouter.FlatStyle = FlatStyle.Flat
        _btnCalcAjouter.BackColor = Color.FromArgb(40, 110, 60) : _btnCalcAjouter.ForeColor = Color.White
        _btnCalcAjouter.Margin = New Padding(0, 0, 6, 0)
        _btnTesterCalc.Text = "▶ Tester" : _btnTesterCalc.Height = 26 : _btnTesterCalc.Width = 76
        _btnTesterCalc.FlatStyle = FlatStyle.Flat
        _btnTesterCalc.BackColor = Color.FromArgb(40, 100, 160) : _btnTesterCalc.ForeColor = Color.White
        _btnTesterCalc.Margin = New Padding(0, 0, 0, 0)
        pnlBtnsCalc.Controls.AddRange({_btnCalcAjouter, _btnTesterCalc})

        ' Bouton toggle aide + zone aide avec AutoScroll pour lire toutes les lignes
        Dim btnAideR As New Button() With {
            .Text = "▼ Aide syntaxe", .Height = 22, .Width = 240,
            .Font = New Font("Segoe UI", 7.5, FontStyle.Bold), .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(200, 215, 240), .ForeColor = Color.FromArgb(20, 40, 80),
            .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(0, 0, 0, 0)}
        btnAideR.FlatAppearance.BorderSize = 0

        Dim pnlAideR As New Panel() With {
            .BackColor = Color.FromArgb(230, 240, 255), .Visible = False,
            .Height = 130, .Width = 240, .Margin = New Padding(0),
            .Padding = New Padding(6, 3, 6, 3), .AutoScroll = True}
        Dim lblAideR As New Label() With {
            .AutoSize = True, .Font = New Font("Segoe UI", 7.5),
            .ForeColor = Color.FromArgb(20, 40, 80), .BackColor = Color.Transparent,
            .Text = "Opérateurs : + − * / ^   abs sqrt ln log10 exp sin cos tan min(a,b) max(a,b)" & vbCrLf &
                    "Constantes : pi  e" & vbCrLf &
                    "Référence colonne : {C2}  (C1=Horodatage, C2=Durée, C3=1ère voie...)" & vbCrLf &
                    "Intégration : int(expr, t_debut, t_fin)  — dt estimé depuis le CSV" & vbCrLf &
                    "  int({C3},,)          intégrale de C3 sur tout le fichier" & vbCrLf &
                    "  int({C3},1800[s],)   depuis t=1800s" & vbCrLf &
                    "  int({C3},30[min],2[h])  entre 30min et 2h" & vbCrLf &
                    "  NE PAS écrire *dt — c'est int() qui l'applique"}
        pnlAideR.Controls.Add(lblAideR)

        ' Toggle aide via Visible
        AddHandler btnAideR.Click, Sub(s, e)
            pnlAideR.Visible = Not pnlAideR.Visible
            btnAideR.Text = If(pnlAideR.Visible, "▲ Aide syntaxe", "▼ Aide syntaxe")
        End Sub

        ' Empiler les contrôles dans pnlEditeur (FlowLayout vertical)
        Dim flowEdit As New FlowLayoutPanel() With {
            .Dock = DockStyle.Top, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False, .AutoScroll = False,
            .BackColor = Color.White, .Padding = New Padding(4, 4, 4, 4)}

        ' Adapter la largeur des contrôles au resize
        AddHandler flowEdit.Resize, Sub(s, e)
            Dim w = Math.Max(100, flowEdit.ClientSize.Width - 8)
            pnlNomUnite.MaximumSize   = New Size(w, 0)
            pnlInserer.MaximumSize    = New Size(w, 0)
            _txtCalcExpr.Width        = w
            pnlBtnsCalc.MaximumSize   = New Size(w, 0)
            btnAideR.Width            = w
            pnlAideR.Width            = w
        End Sub

        flowEdit.Controls.Add(pnlNomUnite)
        flowEdit.Controls.Add(pnlInserer)
        flowEdit.Controls.Add(lblExprCalc)
        flowEdit.Controls.Add(_txtCalcExpr)
        flowEdit.Controls.Add(_lblResultatCalc)
        flowEdit.Controls.Add(pnlBtnsCalc)
        flowEdit.Controls.Add(btnAideR)
        flowEdit.Controls.Add(pnlAideR)

        scrollEditeur.Controls.Add(flowEdit)
        splitCalcs.Panel2.Controls.Add(scrollEditeur)

        pnlCalcs.Controls.Add(splitCalcs)
        tabPageCalcs.Controls.Add(pnlCalcs)

        tabGauche.TabPages.Add(tabPageVoies)
        tabGauche.TabPages.Add(tabPageCalcs)
        splitMain.Panel1.Controls.Add(tabGauche)
        _splitGauche = Nothing  ' remplacé par TabControl

        ' ── Panneau droit : graphique (haut) + tableau (bas) ──
        Dim splitDroite As New SplitContainer() With {
            .Dock = DockStyle.Fill, .Orientation = Orientation.Horizontal}
        AddHandler splitDroite.HandleCreated,
            Sub(sc, ev)
                splitDroite.BeginInvoke(New Action(Sub()
                    Try
                        splitDroite.Panel1MinSize = 100
                        Dim d = Math.Max(100, CInt(splitDroite.Height * 0.65))
                        If d <= splitDroite.Height - 10 Then
                            splitDroite.SplitterDistance = d
                        End If
                    Catch
                    End Try
                End Sub))
            End Sub
        _splitDroite = splitDroite

        _panelGraphe.Dock = DockStyle.Fill
        splitDroite.Panel1.Controls.Add(_panelGraphe)

        _lblStats.Dock = DockStyle.Top : _lblStats.Height = 20
        _lblStats.Font = New Font("Segoe UI", 8, FontStyle.Italic)
        _lblStats.ForeColor = Color.Gray
        _dgvDonnees.Dock = DockStyle.Fill
        _dgvDonnees.ReadOnly = True : _dgvDonnees.AllowUserToAddRows = False
        _dgvDonnees.BackgroundColor = Color.White
        _dgvDonnees.RowHeadersVisible = False
        _dgvDonnees.Font = New Font("Consolas", 8)
        _dgvDonnees.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        _dgvDonnees.ScrollBars = ScrollBars.Both
        splitDroite.Panel2.Controls.Add(_dgvDonnees)
        splitDroite.Panel2.Controls.Add(_lblStats)

        splitMain.Panel2.Controls.Add(splitDroite)
        pnl.Controls.Add(splitMain)  ' Fill — ajouté AVANT tb
        pnl.Controls.Add(tb)         ' Top — ajouté APRÈS pour passer au-dessus

        AddHandler _btnOuvrir.Click,      AddressOf BtnOuvrir_Click
        AddHandler _btnDossier.Click,     AddressOf BtnDossier_Click
        AddHandler _btnRecharger.Click,   Sub(s, e) If _cheminCourant <> "" Then ChargerFichier(_cheminCourant)
        AddHandler _lstSeries.ItemCheck,  AddressOf LstSeries_ItemCheck
        AddHandler _btnTout.Click,        Sub(s, e) CocharTout(True)
        AddHandler _btnAucun.Click,       Sub(s, e) CocharTout(False)
        AddHandler _btnCalcAjouter.Click, AddressOf BtnCalcAjouter_Click
        AddHandler _btnCalcSuppr.Click,   AddressOf BtnCalcSuppr_Click
        AddHandler _btnInsererCalc.Click, AddressOf BtnInsererCalc_Click
        AddHandler _btnTesterCalc.Click,  AddressOf BtnTesterCalc_Click
        AddHandler _lstCalcs.SelectedIndexChanged, AddressOf LstCalcs_SelectedIndexChanged
        AddHandler _lstCalcsVisu.ItemCheck,
            Sub(s, ev)
                ' Délai pour que l'état soit mis à jour avant de reconstruire
                If _cheminCourant <> "" Then
                    _lstCalcsVisu.BeginInvoke(New Action(AddressOf ConstruireHistoriqueEtGraphique))
                End If
            End Sub
        ' Note : on ne branche PAS les TextChanged sur AppliquerEditeurVersCalcVisu
        ' pour éviter que la saisie d'un nouveau calcul modifie le précédent.
        ' La synchronisation se fait uniquement via LstCalcs_SelectedIndexChanged et BtnCalcAjouter.
        AddHandler _btnMasquerCalcs.Click, Sub(s, e)
            ' Dans la nouvelle disposition, l'onglet Calculs est dans tabGauche
            ' Masquer calculs = masquer la tab "Calculs" et réduire le panneau gauche
            Dim collapsed = splitMain.Panel1Collapsed
            splitMain.Panel1Collapsed = Not collapsed
            _btnMasquerCalcs.Text = If(Not collapsed, "🧮 Afficher calculs", "🧮 Masquer calculs")
        End Sub
        AddHandler _btnModeGraphique.Click, Sub(s, e)
            If _panelGraphe.Mode = ModeGraphique.SeriesTemporelles Then
                _panelGraphe.Mode = ModeGraphique.Histogramme
                _btnModeGraphique.Text = "📈 Courbes XY"
            Else
                _panelGraphe.Mode = ModeGraphique.SeriesTemporelles
                _btnModeGraphique.Text = "📊 Histogramme"
            End If
            If _cheminCourant <> "" Then ConstruireHistoriqueEtGraphique()
        End Sub
        AddHandler _btnMasquerTableau.Click, Sub(s, e)
            If _splitDroite IsNot Nothing Then
                _splitDroite.Panel2Collapsed = Not _splitDroite.Panel2Collapsed
                _btnMasquerTableau.Text = If(_splitDroite.Panel2Collapsed,
                    "⊟ Afficher tableau", "⊞ Masquer tableau")
            End If
        End Sub

        Return pnl
    End Function

    ' ─── Ouverture ────────────────────────────────────────────────────────────

    Private Sub BtnOuvrir_Click(sender As Object, e As EventArgs)
        Dim def As String
        If _dernierDossier <> "" AndAlso Directory.Exists(_dernierDossier) Then
            def = _dernierDossier
        ElseIf DossierDefaut <> "" AndAlso Directory.Exists(DossierDefaut) Then
            def = DossierDefaut
        Else
            def = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Thermopilot")
            If Not Directory.Exists(def) Then
                def = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            End If
        End If
        Using dlg As New OpenFileDialog() With {
            .Title  = "Ouvrir un fichier CSV Thermopilot",
            .Filter = "Fichiers CSV|*.csv|Tous|*.*",
            .InitialDirectory = def}
            If dlg.ShowDialog() = DialogResult.OK Then
                _dernierDossier = Path.GetDirectoryName(dlg.FileName)
                ChargerFichier(dlg.FileName)
            End If
        End Using
    End Sub

    Private Sub BtnDossier_Click(sender As Object, e As EventArgs)
        Dim def = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Thermopilot")
        Process.Start("explorer.exe",
            If(Directory.Exists(def), def,
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)))
    End Sub

    ' ─── Chargement CSV ───────────────────────────────────────────────────────

    Public Sub ChargerFichier(chemin As String)
        Try
            Dim toutes = File.ReadAllLines(chemin, System.Text.Encoding.UTF8)

            ' Trouver la première ligne non-commentaire = entête des colonnes
            Dim idxEntete = -1
            For i As Integer = 0 To toutes.Length - 1
                If Not toutes(i).TrimStart().StartsWith("#") AndAlso
                   toutes(i).Trim() <> "" Then
                    idxEntete = i : Exit For
                End If
            Next
            If idxEntete < 0 OrElse toutes.Length - idxEntete < 2 Then
                RaiseEvent StatutChange(Me, "Fichier vide ou invalide.", True) : Return
            End If

            _colonnes.Clear() : _lignes.Clear() : _horodatages.Clear()
            _lstSeries.Items.Clear()
            _indicesColonnes.Clear()
            _dgvDonnees.Columns.Clear() : _dgvDonnees.Rows.Clear()
            _seriesVisu.Clear()

            _separateur = If(toutes(idxEntete).Contains(";"c), ";"c, ","c)
            _colonnes.AddRange(toutes(idxEntete).Split(_separateur))

            For i As Integer = idxEntete + 1 To toutes.Length - 1
                Dim l = toutes(i).Trim()
                If l = "" OrElse l.StartsWith("#") Then Continue For
                Dim cellules = l.Split(_separateur)
                _lignes.Add(cellules)
                Dim dt As DateTime
                Dim ok = DateTime.TryParse(cellules(0).Trim(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.None, dt)
                If Not ok Then ok = DateTime.TryParse(cellules(0).Trim(), dt)
                _horodatages.Add(If(ok, dt, DateTime.MinValue))
            Next

            If _lignes.Count = 0 Then
                RaiseEvent StatutChange(Me, "Aucune donnée dans le fichier.", True) : Return
            End If

            ' Tableau (limité à 2000 lignes)
            For Each col In _colonnes
                _dgvDonnees.Columns.Add(col.Replace(" ", "_"), col)
            Next
            Dim maxR = Math.Min(_lignes.Count, 2000)
            For r As Integer = 0 To maxR - 1
                Dim lig = _lignes(r)
                Dim idx = _dgvDonnees.Rows.Add()
                For c As Integer = 0 To Math.Min(lig.Length - 1, _dgvDonnees.Columns.Count - 1)
                    _dgvDonnees.Rows(idx).Cells(c).Value = lig(c).Trim()
                Next
            Next

            ' Liste des séries (exclure Horodatage et Notification)
            _indicesColonnes.Clear()
            For i As Integer = 1 To _colonnes.Count - 1
                Dim nomC = _colonnes(i).Trim()
                If String.Equals(nomC, "Notification", StringComparison.OrdinalIgnoreCase) Then Continue For
                _lstSeries.Items.Add(_colonnes(i), True)
                _indicesColonnes.Add(i)
            Next

            _cheminCourant = chemin
            _btnRecharger.Enabled = True

            Dim duree = "—"
            If _horodatages.Count > 1 AndAlso
               _horodatages.First() <> DateTime.MinValue AndAlso
               _horodatages.Last() <> DateTime.MinValue Then
                duree = (_horodatages.Last() - _horodatages.First()).TotalMinutes.ToString("F0") & " min"
            End If
            _lblInfo.Text = String.Format("{0} mesures · {1} voies · {2}",
                _lignes.Count, _colonnes.Count - 1, duree)

            AfficherStats()
            ConstruireHistoriqueEtGraphique()
            ActualiserListeColonnes()
            RaiseEvent StatutChange(Me, "CSV chargé : " & Path.GetFileName(chemin), False)

        Catch ex As Exception
            RaiseEvent StatutChange(Me, "Erreur lecture : " & ex.Message, True)
        End Try
    End Sub

    ' ─── Historique et graphique ──────────────────────────────────────────────

    Private Sub ConstruireHistoriqueEtGraphique()
        _historiqueVisu.Vider()

        ' Reconstruire _seriesVisu : voies CSV + calculs
        ' Les séries sont indexées de 1 à N (colonne 0 = Horodatage)
        ' {C1}=Horodatage, {C2}=Durée, {C3}=1ère voie...
        ' → CSV_1=col0, CSV_2=col1, CSV_3=col2...
        ' seriesBase = uniquement les colonnes dans _lstSeries (via _indicesColonnes)
        ' Chaque série : Cle=CSV_{colIdx+1} pour que {C(colIdx+1)} pointe sur cette colonne
        Dim seriesBase As New List(Of PanelGraphique.SerieGraphique)
        For k As Integer = 0 To _indicesColonnes.Count - 1
            Dim colIdx = _indicesColonnes(k)  ' index dans _colonnes
            Dim nomCol = _colonnes(colIdx)
            Dim nomLow = nomCol.ToLowerInvariant()
            ' Sortie booléenne : entête contient "(on/off)"
            Dim estBin      = nomLow.Contains("(on/off)")
            ' Sortie analogique : entête contient "(v)" mais PAS une unité physique comme (°c), (l/h)…
            ' On considère (V) seul (sans autre lettre collée) comme sortie analogique
            Dim estSortieV  = Not estBin AndAlso
                              (nomLow.EndsWith("(v)") OrElse nomLow.Contains(" (v)") OrElse nomLow.Contains("_(v)"))
            Dim cleIdx = colIdx + 1  ' {C(colIdx+1)} correspond à cette colonne
            Dim visible = (k < _lstSeries.Items.Count AndAlso _lstSeries.GetItemChecked(k))
            seriesBase.Add(New PanelGraphique.SerieGraphique() With {
                .Cle          = "CSV_" & cleIdx,
                .Nom          = SupprimerUnite(nomCol),
                .NomCentrale  = "CSV",
                .Unite        = ExtraireUnite(nomCol),
                .EstBinaire   = estBin,
                .EstSortieAnal = estSortieV,
                .Visible      = visible
            })
        Next

        Dim seriesCalc As New List(Of PanelGraphique.SerieGraphique)
        Dim idxCalc As Integer = 0
        For i As Integer = 0 To _gestCalculs.Voies.Count - 1
            Dim vc = _gestCalculs.Voies(i)
            If Not vc.Active Then Continue For
            Dim visible = (i < _lstCalcsVisu.Items.Count AndAlso _lstCalcsVisu.GetItemChecked(i))
            seriesCalc.Add(New PanelGraphique.SerieGraphique() With {
                .Cle         = vc.CleHistorique,
                .Nom         = "[C] " & vc.Nom,
                .NomCentrale = "Calcul#" & idxCalc.ToString(),
                .Unite       = vc.Unite,
                .EstBinaire  = False,
                .Visible     = visible
            })
            idxCalc += 1
        Next

        _seriesVisu = seriesBase.Concat(seriesCalc).ToList()

        ' Créer des voies calculées temporaires avec expressions traduites {Ci}→{CSV_i}
        ' On ne modifie PAS les expressions originales
        Dim gestCalcVisu As New GestionnaireCalculs()
        Dim tmpIdx As Integer = 0
        For Each vcOrig In _gestCalculs.Voies.Where(Function(v) v.Active)
            Dim exprTrad = vcOrig.Expression
            For ci As Integer = 1 To _colonnes.Count
                exprTrad = exprTrad.Replace("{C" & ci & "}", "{CSV_" & ci & "}")
            Next
            ' Utiliser l'ID original pour que CleHistorique corresponde à seriesCalc
            Dim vcTmp As New VoieCalculee() With {
                .Id              = vcOrig.Id,
                .Nom             = vcOrig.Nom & "_visu_" & tmpIdx.ToString(),
                .Unite           = vcOrig.Unite,
                .Expression      = exprTrad,
                .Active          = True,
                .NbPointsMoyenne = vcOrig.NbPointsMoyenne}
            gestCalcVisu.Voies.Add(vcTmp)
            tmpIdx += 1
        Next
        gestCalcVisu.ResetIntegrations()
        Dim dtS = EstimerDt()

        For r As Integer = 0 To _lignes.Count - 1
            Dim lig   = _lignes(r)
            Dim horod = _horodatages(r)
            If horod = DateTime.MinValue Then Continue For

            For i As Integer = 0 To seriesBase.Count - 1
                Dim colIdx = _indicesColonnes(i)  ' index réel dans _colonnes
                If colIdx >= lig.Length Then Continue For
                Dim val As Double
                Dim ok = Double.TryParse(lig(colIdx).Trim(),
                    Globalization.NumberStyles.Float,
                    Globalization.CultureInfo.InvariantCulture, val)
                _historiqueVisu.InjecterPoint(seriesBase(i).Cle, New PointMesure() With {
                    .Horodatage       = horod,
                    .Valeur           = If(ok, val, Double.NaN),
                    .ValeurGraphiqueB = If(seriesBase(i).EstBinaire,
                                          If(ok AndAlso val > 0, 1.0, 0.0),
                                          If(seriesBase(i).EstSortieAnal,
                                             If(ok, val, Double.NaN), Double.NaN)),
                    .EnErreur         = Not ok
                })
            Next
            _historiqueVisu.AjouterHorodatage(horod)

            If gestCalcVisu.Voies.Count > 0 Then
                gestCalcVisu.CalculerEtInjecter(_historiqueVisu, horod, dtS)
            End If
        Next

        _panelGraphe.DefinirSeries(_seriesVisu)
        _panelGraphe.FenetreSecondes = FenetreEnSecondes()
        _panelGraphe.MettreAJour(_historiqueVisu)
    End Sub

    Private Function EstimerDt() As Double
        Dim diffs As New List(Of Double)
        For i As Integer = 1 To Math.Min(20, _horodatages.Count - 1)
            If _horodatages(i) <> DateTime.MinValue AndAlso
               _horodatages(i - 1) <> DateTime.MinValue Then
                Dim d = (_horodatages(i) - _horodatages(i - 1)).TotalSeconds
                If d > 0 Then diffs.Add(d)
            End If
        Next
        Return If(diffs.Count > 0, diffs.Average(), 5.0)
    End Function

    Private Function FenetreEnSecondes() As Integer
        Return 0  ' Pas de fenêtre glissante dans l'onglet Résultats
    End Function

    Private Sub MettreAJourFenetre()
        _panelGraphe.FenetreSecondes = FenetreEnSecondes()
        _panelGraphe.MettreAJour(_historiqueVisu)
    End Sub

    ' ─── Sélection des séries ─────────────────────────────────────────────────

    Private Sub LstSeries_ItemCheck(sender As Object, e As ItemCheckEventArgs)
        Application.DoEvents()
        If e.Index < _seriesVisu.Count Then
            _seriesVisu(e.Index).Visible = (e.NewValue = CheckState.Checked)
            _panelGraphe.SetVisible(_seriesVisu(e.Index).Cle, e.NewValue = CheckState.Checked)
        End If
    End Sub

    Private Sub CocharTout(etat As Boolean)
        For i As Integer = 0 To _lstSeries.Items.Count - 1
            _lstSeries.SetItemChecked(i, etat)
            If i < _seriesVisu.Count Then
                _seriesVisu(i).Visible = etat
                _panelGraphe.SetVisible(_seriesVisu(i).Cle, etat)
            End If
        Next
    End Sub

    ' ─── Calculs utilisateur ──────────────────────────────────────────────────

    ' ─── Calculs — contrôles complémentaires ──────────────────────────────────

    Private Sub ActualiserListeColonnes()
        _cmbColonnes.Items.Clear()
        For i As Integer = 0 To _colonnes.Count - 1
            _cmbColonnes.Items.Add("{C" & (i + 1) & "} : " & _colonnes(i))
        Next
        If _cmbColonnes.Items.Count > 0 Then _cmbColonnes.SelectedIndex = 0
    End Sub

    Private Sub BtnInsererCalc_Click(sender As Object, e As EventArgs)
        If _cmbColonnes.SelectedIndex < 0 Then Return
        Dim ref = "{C" & (_cmbColonnes.SelectedIndex + 1) & "}"
        Dim pos = _txtCalcExpr.SelectionStart
        _txtCalcExpr.Text = _txtCalcExpr.Text.Insert(pos, ref)
        _txtCalcExpr.SelectionStart = pos + ref.Length
        _txtCalcExpr.Focus()
    End Sub

    Private Sub BtnTesterCalc_Click(sender As Object, e As EventArgs)
        AppliquerEditeurVersCalcVisu()
        If _indexEditeCalc < 0 OrElse _indexEditeCalc >= _gestCalculs.Voies.Count Then
            _lblResultatCalc.Text = "Aucun calcul sélectionné."
            _lblResultatCalc.ForeColor = Color.OrangeRed : Return
        End If
        Dim vc = _gestCalculs.Voies(_indexEditeCalc)
        If String.IsNullOrWhiteSpace(vc.Expression) Then
            _lblResultatCalc.Text = "Expression vide."
            _lblResultatCalc.ForeColor = Color.OrangeRed : Return
        End If
        ' Traduire {C1}→{CSV_1} pour le test
        Dim exprOrig = vc.Expression
        For ci As Integer = 1 To _colonnes.Count - 1
            vc.Expression = vc.Expression.Replace("{C" & ci & "}", "{CSV_" & ci & "}")
        Next
        Try
            vc.ResetIntegration()
            vc.Calculer(_historiqueVisu)
            If vc.EnErreur Then
                _lblResultatCalc.Text = "Erreur : " & vc.MessageErreur
                _lblResultatCalc.ForeColor = Color.OrangeRed
            Else
                _lblResultatCalc.Text = "= " & vc.Valeur.ToString("G6") & " " & vc.Unite
                _lblResultatCalc.ForeColor = Color.FromArgb(0, 140, 70)
            End If
        Catch ex As Exception
            _lblResultatCalc.Text = "Erreur : " & ex.Message
            _lblResultatCalc.ForeColor = Color.OrangeRed
        Finally
            vc.Expression = exprOrig  ' restaurer
        End Try
    End Sub

    Private Sub LstCalcs_SelectedIndexChanged(sender As Object, e As EventArgs)
        Dim idx = _lstCalcs.SelectedIndex
        If idx < 0 OrElse idx >= _gestCalculs.Voies.Count Then Return
        _chargementCalc = True
        _indexEditeCalc = idx
        Dim vc = _gestCalculs.Voies(idx)
        _txtCalcNom.Text   = vc.Nom
        _txtCalcUnite.Text = vc.Unite
        _txtCalcExpr.Text  = vc.Expression
        _numCalcMoy.Value  = CInt(vc.NbPointsMoyenne)
        _lblResultatCalc.Text = ""
        _chargementCalc = False
    End Sub

    Private Sub AppliquerEditeurVersCalcVisu()
        If _chargementCalc Then Return
        If _indexEditeCalc < 0 OrElse _indexEditeCalc >= _gestCalculs.Voies.Count Then Return
        Dim vc = _gestCalculs.Voies(_indexEditeCalc)
        vc.Nom             = _txtCalcNom.Text.Trim()
        vc.Unite           = _txtCalcUnite.Text.Trim()
        vc.Expression      = _txtCalcExpr.Text.Trim()
        vc.NbPointsMoyenne = CInt(_numCalcMoy.Value)
        ' Mettre à jour la listbox sans déclencher SelectedIndexChanged
        RemoveHandler _lstCalcs.SelectedIndexChanged, AddressOf LstCalcs_SelectedIndexChanged
        If _indexEditeCalc < _lstCalcs.Items.Count Then
            _lstCalcs.Items(_indexEditeCalc) = If(vc.Nom <> "", vc.Nom, "(calcul " & (_indexEditeCalc+1) & ")")
        End If
        AddHandler _lstCalcs.SelectedIndexChanged, AddressOf LstCalcs_SelectedIndexChanged
    End Sub

        Private Sub BtnCalcAjouter_Click(sender As Object, e As EventArgs)
        Dim nom   = _txtCalcNom.Text.Trim()
        Dim expr  = _txtCalcExpr.Text.Trim()
        If nom = "" OrElse expr = "" Then
            MessageBox.Show("Nom et expression requis.", "Calcul",
                MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim moy = CInt(_numCalcMoy.Value)
        Dim uni = _txtCalcUnite.Text.Trim()

        ' Vider l'éditeur AVANT de créer le calcul (évite synchro parasite via TextChanged)
        _chargementCalc = True
        _indexEditeCalc = -1
        _txtCalcNom.Clear() : _txtCalcExpr.Clear() : _txtCalcUnite.Clear()
        _numCalcMoy.Value = 1
        _chargementCalc = False

        ' Générer un ID unique (avec pause pour garantir unicité timestamp)
        System.Threading.Thread.Sleep(10)
        Dim vc As New VoieCalculee() With {
            .Id              = GestionnaireCalculs.NouvelId(),
            .Nom             = nom,
            .Unite           = uni,
            .Expression      = expr,
            .Active          = True,
            .NbPointsMoyenne = moy
        }
        _gestCalculs.Voies.Add(vc)
        _indexEditeCalc = _gestCalculs.Voies.Count - 1
        Dim nomAff = If(nom <> "", nom, "(calcul " & _gestCalculs.Voies.Count & ")")
        _lstCalcs.Items.Add(nomAff)
        ' Sélectionner sans déclencher SelectedIndexChanged
        RemoveHandler _lstCalcs.SelectedIndexChanged, AddressOf LstCalcs_SelectedIndexChanged
        _lstCalcs.SelectedIndex = _lstCalcs.Items.Count - 1
        AddHandler _lstCalcs.SelectedIndexChanged, AddressOf LstCalcs_SelectedIndexChanged
        _lstCalcsVisu.Items.Add(nomAff, True)

        If _cheminCourant <> "" Then ConstruireHistoriqueEtGraphique()
    End Sub

    Private Sub BtnCalcSuppr_Click(sender As Object, e As EventArgs)
        Dim idx = _lstCalcs.SelectedIndex
        If idx < 0 Then Return
        _gestCalculs.Voies.RemoveAt(idx)
        _lstCalcs.Items.RemoveAt(idx)
        ' Supprimer aussi de la liste Mesures
        If idx < _lstCalcsVisu.Items.Count Then
            _lstCalcsVisu.Items.RemoveAt(idx)
        End If
        _indexEditeCalc = -1
        If _cheminCourant <> "" Then ConstruireHistoriqueEtGraphique()
    End Sub

    ' ─── Statistiques ─────────────────────────────────────────────────────────

    Private Sub AfficherStats()
        If _lignes.Count = 0 OrElse _colonnes.Count < 2 Then Return
        Dim sb As New System.Text.StringBuilder()
        For c As Integer = 1 To Math.Min(_colonnes.Count - 1, 6)
            Dim vals As New List(Of Double)
            For Each lig In _lignes
                If c >= lig.Length Then Continue For
                Dim d As Double
                If Double.TryParse(lig(c).Trim(), Globalization.NumberStyles.Float,
                                   Globalization.CultureInfo.InvariantCulture, d) Then
                    vals.Add(d)
                End If
            Next
            If vals.Count > 0 Then
                Dim n = SupprimerUnite(_colonnes(c))
                If n.Length > 12 Then n = n.Substring(0, 12) & "…"
                sb.AppendFormat("{0} [min:{1:F2} max:{2:F2} moy:{3:F2}]   ",
                    n, vals.Min(), vals.Max(), vals.Average())
            End If
        Next
        If _colonnes.Count > 7 Then sb.Append("…")
        _lblStats.Text = sb.ToString()
    End Sub

    ' ─── Utilitaires ──────────────────────────────────────────────────────────

    Private Shared Function ExtraireUnite(nomCol As String) As String
        Dim debut = nomCol.LastIndexOf("("c)
        Dim fin   = nomCol.LastIndexOf(")"c)
        If debut >= 0 AndAlso fin > debut Then
            Return nomCol.Substring(debut + 1, fin - debut - 1).Trim()
        End If
        Return ""
    End Function

    Private Shared Function SupprimerUnite(nomCol As String) As String
        Dim debut = nomCol.LastIndexOf("("c)
        If debut > 0 Then Return nomCol.Substring(0, debut).Trim()
        Return nomCol.Trim()
    End Function

End Class
