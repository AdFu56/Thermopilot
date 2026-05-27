Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualBasic
Imports OxyPlot
Imports OxyPlot.Axes
Imports OxyPlot.Series
Imports OxyPlot.WindowsForms
Imports OxyPlot.Legends

''' <summary>
''' Graphique multi-voies multi-centrales basé sur OxyPlot.
''' API publique identique à l'ancienne version GDI+ :
'''   - DefinirSeries()  : déclare les séries à tracer
'''   - SetVisible()     : affiche/masque une série
'''   - MettreAJour()    : rafraîchit les données depuis l'historique
'''   - ExporterPNG()    : exporte le graphique en PNG
''' Fonctionnalités ajoutées par OxyPlot :
'''   - Zoom molette souris sur X et Y
'''   - Pan clic-glisser
'''   - Tooltip valeur + horodatage au survol
'''   - Réinitialisation zoom double-clic ou menu contextuel
'''   - Axe Y indépendant par série analogique (couleur associée)
'''   - Axe Y secondaire (droite) pour les sorties binaires 0/1
''' </summary>
Public Enum ModeGraphique
    SeriesTemporelles = 0
    Histogramme       = 1
End Enum

Public Class PanelGraphique
    Inherits UserControl

    ' ── Modèle public (compatible avec l'ancienne API) ─────────────────────────

    Public Class SerieGraphique
        Public Property Cle         As String
        Public Property Nom         As String
        Public Property NomCentrale As String
        Public Property Unite         As String
        Public Property EstBinaire    As Boolean
        Public Property EstSortieAnal As Boolean = False   ' sortie analogique (axe V dédié)
        Public Property Visible       As Boolean = True
        Public Property Couleur     As Color
        Public Property IndexAxeY   As Integer = 0
    End Class

    ' ── Données ────────────────────────────────────────────────────────────────

    Private _series     As New List(Of SerieGraphique)
    Private _historique As HistoriqueMultiCentrale

    Private ReadOnly _plotView  As New PlotView()
    Private ReadOnly _plotModel As New PlotModel()

    Private _oxySeries As New Dictionary(Of String, XYAxisSeries)
    Private _oxyAxes   As New Dictionary(Of String, LinearAxis)

    ''' <summary>Référence au ConfigManager — nécessaire pour charger les styles par série au bon moment.</summary>
    Public Property Config As ConfigManager
        Get
            Return _configPourStyles
        End Get
        Set(value As ConfigManager)
            _configPourStyles = value
            ' Charger les styles globaux immédiatement
            If value IsNot Nothing Then
                Styles.ChargerDepuisConfig(value)
                RebuildAxes()
                _plotModel.InvalidatePlot(True)
            End If
        End Set
    End Property
    Private _configPourStyles As ConfigManager = Nothing

    ''' <summary>Styles visuels personnalisés — partagé avec FormPersonnalisationGraphique.</summary>
    Public ReadOnly Styles As New StylesGraphique()

    ''' <summary>
    ''' Durée de la fenêtre glissante en secondes.
    ''' 0 = afficher tout l'historique (pas de fenêtre glissante).
    ''' </summary>
    Public Property FenetreSecondes As Integer = 0
    Private _modeActif As ModeGraphique = ModeGraphique.SeriesTemporelles
    Public Property Mode As ModeGraphique
        Get
            Return _modeActif
        End Get
        Set(value As ModeGraphique)
            If value = _modeActif Then Return
            _modeActif = value
            ' Vider complètement le modèle lors du changement de mode
            _plotModel.Series.Clear()
            _oxySeries.Clear()
            _plotModel.Axes.Clear()
            _oxyAxes.Clear()
            If value = ModeGraphique.SeriesTemporelles Then
                ' Remettre l'axe X DateTimeAxis
                Dim axeX As New DateTimeAxis() With {
                    .Position = OxyPlot.Axes.AxisPosition.Bottom,
                    .Key      = "X",
                    .StringFormat = "HH:mm:ss",
                    .TextColor    = Styles.CouleurTexte,
                    .TicklineColor = Styles.CouleurTexte}
                _plotModel.Axes.Add(axeX)
                If _series.Count > 0 Then DefinirSeries(_series)
            End If
            _plotModel.InvalidatePlot(True)
        End Set
    End Property

    Private Shared ReadOnly _palette As Color() = {
        Color.FromArgb(220, 80,  50),  Color.FromArgb(30,  130, 210),
        Color.FromArgb(50,  185, 80),  Color.FromArgb(220, 155, 20),
        Color.FromArgb(155, 60,  215), Color.FromArgb(20,  195, 185),
        Color.FromArgb(215, 65,  155), Color.FromArgb(110, 110, 110),
        Color.FromArgb(175, 95,  20),  Color.FromArgb(85,  175, 215),
        Color.FromArgb(185, 225, 80),  Color.FromArgb(215, 105, 105),
        Color.FromArgb(105, 215, 155), Color.FromArgb(105, 105, 215),
        Color.FromArgb(215, 175, 55),  Color.FromArgb(55,  215, 215)
    }
    Private Shared ReadOnly _paletteBinaire As Color() = {
        Color.FromArgb(255, 140, 0),   Color.FromArgb(200, 60,  200),
        Color.FromArgb(0,   180, 120), Color.FromArgb(180, 50,  50),
        Color.FromArgb(50,  50,  200)
    }

    ' ── Constructeur ───────────────────────────────────────────────────────────

    Public Sub New()
        Me.Dock = DockStyle.Fill

        ' Apparence du modèle
        _plotModel.Background          = OxyColor.FromRgb(28, 30, 42)
        _plotModel.PlotAreaBorderColor = OxyColor.FromRgb(60, 65, 80)
        _plotModel.TextColor           = OxyColor.FromRgb(180, 190, 210)
        _plotModel.PlotMargins         = New OxyThickness(Double.NaN, Double.NaN, Double.NaN, 40)

        ' Légende — Inside TopRight par défaut : ne masque pas les données basses,
        ' et ne crée pas de problème de troncature comme Outside
        Dim leg As New Legend() With {
            .LegendPosition      = LegendPosition.TopRight,
            .LegendOrientation   = LegendOrientation.Vertical,
            .LegendPlacement     = LegendPlacement.Inside,
            .LegendBackground    = OxyColor.FromArgb(200, 30, 32, 45),
            .LegendBorder        = OxyColor.FromRgb(60, 65, 80),
            .LegendBorderThickness = 1,
            .LegendTextColor     = OxyColor.FromRgb(180, 190, 210),
            .LegendFontSize      = 9,
            .LegendMargin        = 8,
            .LegendPadding       = 6,
            .LegendItemSpacing   = 4,
            .LegendColumnSpacing = 12,
            .LegendMaxWidth      = Double.NaN,
            .LegendMaxHeight     = Double.NaN
        }
        _plotModel.Legends.Add(leg)

        ' Axe X temps
        _plotModel.Axes.Add(New DateTimeAxis() With {
            .Position            = AxisPosition.Bottom,
            .StringFormat        = "HH:mm:ss",
            .TicklineColor       = OxyColor.FromRgb(60, 65, 80),
            .TextColor           = OxyColor.FromRgb(140, 150, 170),
            .AxislineColor       = OxyColor.FromRgb(60, 65, 80),
            .MajorGridlineStyle  = LineStyle.Dot,
            .MajorGridlineColor  = OxyColor.FromArgb(80, 40, 44, 60),
            .MinorGridlineStyle  = LineStyle.None,
            .Key                 = "X"
        })

        ' Contrôle
        _plotView.Model     = _plotModel
        _plotView.Dock      = DockStyle.Fill
        _plotView.BackColor = Color.FromArgb(28, 30, 42)

        ' Interactions souris
        Dim ctrl As New PlotController()
        ctrl.BindMouseWheel(PlotCommands.ZoomWheel)
        ctrl.BindMouseDown(OxyMouseButton.Left,  PlotCommands.PanAt)
        ctrl.BindMouseDown(OxyMouseButton.Right, PlotCommands.ZoomRectangle)
        ctrl.BindMouseEnter(PlotCommands.HoverPointsOnlyTrack)
        _plotView.Controller = ctrl

        ' Menu contextuel
        Dim menu         As New ContextMenuStrip()
        Dim mExport      As New ToolStripMenuItem("📷  Exporter en PNG…")
        Dim mReset       As New ToolStripMenuItem("⟳  Réinitialiser le zoom")
        Dim mPerso       As New ToolStripMenuItem("⚙  Personnaliser le graphique…")
        Dim mRAZ         As New ToolStripMenuItem("↺  RAZ personnalisation")
        AddHandler mExport.Click, Sub(s, e) ExporterPNG()
        AddHandler mReset.Click,  Sub(s, e) ResetZoom()
        AddHandler mPerso.Click,  Sub(s, e) OuvrirPersonnalisation()
        AddHandler mRAZ.Click,    Sub(s, e) RAZPersonnalisation()
        menu.Items.AddRange({mExport, mReset, New ToolStripSeparator(), mPerso, mRAZ})
        _plotView.ContextMenuStrip = menu

        Me.Controls.Add(_plotView)
    End Sub

    ' ── API publique ───────────────────────────────────────────────────────────

    Public Sub DefinirSeries(series As List(Of SerieGraphique))
        _series = New List(Of SerieGraphique)(series)
        _oxySeries.Clear()
        _oxyAxes.Clear()
        _plotModel.Series.Clear()

        ' Conserver uniquement l'axe X
        Dim axeX = _plotModel.Axes.FirstOrDefault(Function(a) a.Key = "X")
        _plotModel.Axes.Clear()
        If axeX IsNot Nothing Then _plotModel.Axes.Add(axeX)

        ' Assigner couleurs par défaut (seront écrasées si un style sauvegardé existe)
        Dim idxAnal = 0
        Dim idxBin  = 0
        For Each sg In _series
            If sg.EstBinaire Then
                sg.Couleur   = _paletteBinaire(idxBin Mod _paletteBinaire.Length)
                sg.IndexAxeY = -1
                idxBin += 1
            Else
                sg.Couleur   = _palette(idxAnal Mod _palette.Length)
                sg.IndexAxeY = idxAnal
                idxAnal += 1
            End If
        Next

        ' Recharger les styles par série depuis la config (si un ConfigManager est disponible)
        ' — les clés de séries sont connues maintenant, on peut les charger
        If _configPourStyles IsNot Nothing Then
            For Each sg In _series
                Dim coulDef = OxyColor.FromRgb(sg.Couleur.R, sg.Couleur.G, sg.Couleur.B)
                Styles.ChargerStyleSerie(_configPourStyles, sg.Cle, coulDef)
                ' Mettre à jour la couleur du SerieGraphique depuis le style chargé
                Dim style = Styles.ObtenirStyle(sg.Cle, sg.Couleur)
                sg.Couleur = Color.FromArgb(style.Couleur.R, style.Couleur.G, style.Couleur.B)
            Next
        End If

        ' Créer les séries OxyPlot — YAxisKey vide si invisible (pas d'axe créé)
        For Each sg In _series
            If sg.EstBinaire Then
                Dim oxyS As New LineSeries() With {
                    .Title               = String.Format("[{0}] {1}", sg.NomCentrale, sg.Nom),
                    .Color               = ToOxy(sg.Couleur),
                    .StrokeThickness     = 2.2,
                    .IsVisible           = sg.Visible,
                    .XAxisKey            = "X",
                    .YAxisKey            = If(sg.Visible, "YBin", ""),
                    .TrackerFormatString = "{0}" & Chr(10) & "Heure : {2:HH:mm:ss}" & Chr(10) & "État : {4:F0}"
                }
                _plotModel.Series.Add(oxyS)
                _oxySeries(sg.Cle) = oxyS
            Else
                Dim oxyS As New LineSeries() With {
                    .Title               = String.Format("[{0}] {1} ({2})", sg.NomCentrale, sg.Nom, sg.Unite),
                    .Color               = ToOxy(sg.Couleur),
                    .StrokeThickness     = 1.8,
                    .IsVisible           = sg.Visible,
                    .XAxisKey            = "X",
                    .YAxisKey            = "",   ' sera assigné par RebuildAxes
                    .MarkerType          = MarkerType.None,
                    .TrackerFormatString = "{0}" & Chr(10) & "Heure : {2:HH:mm:ss}" & Chr(10) & "Valeur : {4:F3} " & sg.Unite
                }
                _plotModel.Series.Add(oxyS)
                _oxySeries(sg.Cle) = oxyS
            End If
        Next

        ' Construire les axes uniquement pour les séries visibles
        RebuildAxes()
        _plotModel.InvalidatePlot(True)
    End Sub

    ''' <summary>
    ''' Reconstruit les axes Y uniquement pour les séries visibles.
    ''' Applique aussi les styles globaux (fond, grille, texte).
    ''' </summary>
    Private Sub RebuildAxes()
        Dim axeX = _plotModel.Axes.FirstOrDefault(Function(a) a.Key = "X")
        _plotModel.Axes.Clear()
        _oxyAxes.Clear()
        If axeX IsNot Nothing Then _plotModel.Axes.Add(axeX)

        ' Ajouter un axe Y par défaut (clé vide) pour les séries masquées.
        ' OxyPlot cherche un axe avec YAxisKey="" pour toute série masquée —
        ' sans cet axe il lève "Cannot find axis with Key = """"
        _plotModel.Axes.Add(New LinearAxis() With {
            .Key      = "",
            .Position = AxisPosition.Left,
            .IsAxisVisible = False,   ' invisible mais présent pour OxyPlot
            .Minimum  = Double.NaN,
            .Maximum  = Double.NaN
        })

        ' Styles globaux
        _plotModel.Background         = Styles.CouleurFond
        _plotModel.PlotAreaBackground = Styles.CouleurFond
        _plotModel.TextColor          = Styles.CouleurTexte
        _plotView.BackColor           = Color.FromArgb(Styles.CouleurFond.A,
                                            Styles.CouleurFond.R,
                                            Styles.CouleurFond.G,
                                            Styles.CouleurFond.B)
        ' Laisser OxyPlot calculer automatiquement les marges selon la légende
        _plotModel.PlotMargins = New OxyThickness(Double.NaN, Double.NaN, Double.NaN, Styles.MargeBasse)
        ' Styles légende
        If _plotModel.Legends.Count > 0 Then
            Dim leg = _plotModel.Legends(0)
            leg.LegendBackground    = Styles.CouleurFondLegende
            leg.LegendTextColor     = Styles.CouleurTexteLegende
            leg.LegendBorder        = Styles.CouleurBordureLegende
            leg.LegendFontSize      = Styles.TaillePoliceLegende
            leg.LegendPosition      = Styles.PositionLegende
            leg.LegendPlacement     = Styles.PlacementLegende
            leg.IsLegendVisible     = Styles.LegendeVisible
            leg.LegendMargin        = Styles.MargeLegende
            leg.LegendPadding       = Styles.PaddingLegende
            leg.LegendMaxWidth      = Double.NaN   ' jamais tronquée en largeur
            leg.LegendMaxHeight     = Double.NaN   ' jamais tronquée en hauteur
            leg.LegendItemSpacing   = 8
            leg.LegendColumnSpacing = 12
        End If
        ' Grille sur axe X
        Dim ax = TryCast(axeX, OxyPlot.Axes.DateTimeAxis)
        If ax IsNot Nothing Then
            ax.MajorGridlineStyle = Styles.StyleGrille
            ax.MajorGridlineColor = Styles.CouleurGrille
            ax.TextColor          = Styles.CouleurTexte
            ax.FontSize           = Styles.TaillePoliceAxes
            ax.TitleFontSize      = Styles.TaillePoliceAxesTitre
        End If

        Dim seriesAnalVisibles = _series.Where(Function(s) Not s.EstBinaire AndAlso s.Visible).ToList()
        Dim seriesBinVisibles  = _series.Where(Function(s) s.EstBinaire AndAlso s.Visible).ToList()

        ' ── Axes Y : un axe par UNITÉ (pas un par série) ──
        ' Les séries de même unité partagent le même axe → moins d'axes, graphique lisible

        If seriesBinVisibles.Count > 0 Then
            _plotModel.Axes.Add(New LinearAxis() With {
                .Position           = AxisPosition.Right,
                .Minimum            = -0.1,
                .Maximum            = 1.3,
                .IsZoomEnabled      = False,
                .IsPanEnabled       = False,
                .TicklineColor      = OxyColor.FromRgb(80, 75, 50),
                .TextColor          = Styles.CouleurTexte,
                .AxislineColor      = OxyColor.FromRgb(80, 75, 50),
                .MajorGridlineStyle = LineStyle.None,
                .Key                = "YBin",
                .Title              = "ON/OFF",
                .TitleColor         = OxyColor.FromRgb(180, 150, 80),
                .FontSize           = Styles.TaillePoliceAxes,
                .TitleFontSize      = Styles.TaillePoliceAxesTitre,
                .MajorStep          = 1
            })
        End If

        ' Construire un axe par unité distincte
        Dim unitesVues As New Dictionary(Of String, String)  ' unité → clé d'axe
        Dim tier = 0
        For Each sg In seriesAnalVisibles
            Dim unite  = If(String.IsNullOrEmpty(sg.Unite), "?", sg.Unite)
            Dim axeKey As String

            If unitesVues.ContainsKey(unite) Then
                ' Unité déjà vue → réutiliser l'axe existant
                axeKey = unitesVues(unite)
            Else
                ' Nouvelle unité → créer un axe
                axeKey = "Y_" & unite.Replace(" ", "_").Replace("/", "_")
                unitesVues(unite) = axeKey

                Dim style     = Styles.ObtenirStyle(sg.Cle, sg.Couleur)
                Dim coulAxe   = style.Couleur   ' couleur de la première série de cette unité
                Dim estPremier = (tier = 0)

                Dim axeY As New LinearAxis() With {
                    .Position           = AxisPosition.Left,
                    .PositionTier       = tier,
                    .AxislineColor      = coulAxe,
                    .TicklineColor      = coulAxe,
                    .TextColor          = coulAxe,
                    .TitleColor         = coulAxe,
                    .Title              = unite,
                    .FontSize           = Styles.TaillePoliceAxes,
                    .TitleFontSize      = Styles.TaillePoliceAxesTitre,
                    .MajorGridlineStyle = If(estPremier, Styles.StyleGrille, LineStyle.None),
                    .MajorGridlineColor = Styles.CouleurGrille,
                    .MinorGridlineStyle = LineStyle.None,
                    .Key                = axeKey,
                    .IsZoomEnabled      = True,
                    .IsPanEnabled       = True,
                    .Minimum            = Double.NaN,
                    .Maximum            = Double.NaN,
                    .MinimumPadding     = 0.1,
                    .MaximumPadding     = 0.1
                }
                _plotModel.Axes.Add(axeY)
                tier += 1
            End If

            ' Mettre à jour la YAxisKey de la série OxyPlot
            _oxyAxes(sg.Cle) = TryCast(
                _plotModel.Axes.FirstOrDefault(Function(a) a.Key = axeKey),
                LinearAxis)
            If _oxySeries.ContainsKey(sg.Cle) Then
                Dim ls = TryCast(_oxySeries(sg.Cle), LineSeries)
                If ls IsNot Nothing Then ls.YAxisKey = axeKey
            End If
        Next

        AppliquerStylesSurSeries()
    End Sub

    ''' <summary>Applique les styles personnalisés (couleur, ligne, marqueur, épaisseur) sur chaque LineSeries.</summary>
    Private Sub AppliquerStylesSurSeries()
        For Each sg In _series
            If Not _oxySeries.ContainsKey(sg.Cle) Then Continue For
            Dim ls = TryCast(_oxySeries(sg.Cle), LineSeries)
            If ls Is Nothing Then Continue For
            Dim style = Styles.ObtenirStyle(sg.Cle, sg.Couleur)
            ls.Color           = style.Couleur
            ls.StrokeThickness = style.Epaisseur
            ls.LineStyle       = style.StyleLigne
            ls.MarkerType      = style.Marqueur
            ls.MarkerSize      = style.TailleMarqueur
            ls.MarkerFill      = style.Couleur
            ls.MarkerStroke    = style.Couleur
        Next
    End Sub

    ''' <summary>Ouvre la fenêtre de personnalisation du graphique.</summary>
    Public Sub OuvrirPersonnalisation()
        Using frm As New FormPersonnalisationGraphique(
                Styles, _series,
                Sub()
                    RebuildAxes()
                    _plotModel.InvalidatePlot(True)
                End Sub)
            frm.ShowDialog(_plotView.FindForm())
        End Using
    End Sub

    ''' <summary>RAZ complète des personnalisations aux valeurs par défaut.</summary>
    Public Sub RAZPersonnalisation()
        Dim rep = MessageBox.Show(
            "Réinitialiser toutes les personnalisations du graphique ?",
            "RAZ personnalisation", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If rep <> DialogResult.Yes Then Return
        Styles.ResetTout(_series)
        RebuildAxes()
        _plotModel.InvalidatePlot(True)
    End Sub

    Public Sub SetVisible(cle As String, visible As Boolean)
        Dim sg = _series.FirstOrDefault(Function(s) s.Cle = cle)
        If sg Is Nothing Then Return
        sg.Visible = visible

        If _oxySeries.ContainsKey(cle) Then
            Dim ls = TryCast(_oxySeries(cle), LineSeries)
            If ls IsNot Nothing Then
                ls.IsVisible = visible
                ' Séries masquées → axe par défaut (clé vide, invisible)
                ' Séries visibles → RebuildAxes leur assignera le bon axe
                ls.YAxisKey = If(visible, "", "")   ' toujours "" — RebuildAxes réassigne
            End If
        End If

        RebuildAxes()
        If _historique IsNot Nothing Then MettreAJour(_historique)
        _plotModel.InvalidatePlot(True)
    End Sub

    Public Sub MettreAJour(historique As HistoriqueMultiCentrale)
        If Mode = ModeGraphique.Histogramme Then
            MettreAJourHistogramme(historique)
            Return
        End If
        _historique = historique
        If historique Is Nothing Then Return

        For Each sg In _series
            If Not _oxySeries.ContainsKey(sg.Cle) Then Continue For
            Dim oxyS = TryCast(_oxySeries(sg.Cle), LineSeries)
            If oxyS Is Nothing Then Continue For
            Dim pts  = historique.ObtenirSerie(sg.Cle)
            oxyS.Points.Clear()
            If sg.EstBinaire Then
                ' Simuler le rendu en escalier en doublant chaque point
                Dim prevVal As Double = 0
                For Each pt In pts
                    If pt.EnErreur Then Continue For
                    Dim val = If(Not Double.IsNaN(pt.ValeurGraphiqueB), pt.ValeurGraphiqueB, 0.0)
                    Dim t   = DateTimeAxis.ToDouble(pt.Horodatage)
                    ' Point vertical (même t, valeur précédente → valeur actuelle)
                    oxyS.Points.Add(New DataPoint(t, prevVal))
                    oxyS.Points.Add(New DataPoint(t, val))
                    prevVal = val
                Next
            Else
                For Each pt In pts
                    If pt.EnErreur OrElse Double.IsNaN(pt.Valeur) Then Continue For
                    oxyS.Points.Add(New DataPoint(DateTimeAxis.ToDouble(pt.Horodatage), pt.Valeur))
                Next
            End If
        Next

        ' ── Réinitialiser les bornes Y pour que OxyPlot les recalcule depuis les données ──
        For Each axeY In _plotModel.Axes.OfType(Of LinearAxis)()
            If axeY.Key = "YBin" Then Continue For  ' axe binaire : bornes fixes
            axeY.Minimum = Double.NaN
            axeY.Maximum = Double.NaN
        Next

        ' ── Axe X : fenêtre glissante ou auto-scale ──
        Dim axeX = TryCast(_plotModel.Axes.FirstOrDefault(Function(a) a.Key = "X"), DateTimeAxis)
        If axeX IsNot Nothing Then
            Dim dates = _historique.ObtenirHorodatages()
            If dates.Count >= 2 Then
                Dim tMax = dates.Last()
                If FenetreSecondes > 0 Then
                    ' Fenêtre glissante : bornes fixes
                    axeX.Minimum = DateTimeAxis.ToDouble(tMax.AddSeconds(-FenetreSecondes))
                    axeX.Maximum = DateTimeAxis.ToDouble(tMax)
                Else
                    ' Tout l'historique : auto-scale
                    axeX.Minimum = Double.NaN
                    axeX.Maximum = Double.NaN
                End If
            End If
        End If

        ' True = recalcul complet des bornes depuis les données
        _plotModel.InvalidatePlot(True)
    End Sub

    ''' <summary>Affiche un histogramme : une barre par série, valeur = dernière mesure disponible.</summary>
    Private Sub MettreAJourHistogramme(historique As HistoriqueMultiCentrale)
        _historique = historique
        If historique Is Nothing Then Return

        _plotModel.Series.Clear()
        _plotModel.Axes.Clear()

        Dim seriesVisibles = _series.Where(Function(sg) sg.Visible AndAlso Not sg.EstBinaire).ToList()
        If seriesVisibles.Count = 0 Then
            _plotModel.InvalidatePlot(True) : Return
        End If

        ' LinearBarSeries = barres VERTICALES dans OxyPlot 2.1.2
        ' (ColumnSeries n'existe pas dans cette version)
        Dim axeX As New LinearAxis() With {
            .Position = OxyPlot.Axes.AxisPosition.Bottom,
            .IsAxisVisible = True,
            .TextColor = Styles.CouleurTexte,
            .TicklineColor = Styles.CouleurTexte,
            .MajorStep = 1, .MinorStep = 1,
            .Minimum = -0.5, .Maximum = seriesVisibles.Count - 0.5}
        Dim axeVal As New LinearAxis() With {
            .Position = OxyPlot.Axes.AxisPosition.Left,
            .TextColor = Styles.CouleurTexte,
            .TicklineColor = Styles.CouleurTexte,
            .MajorGridlineStyle = Styles.StyleGrille,
            .MajorGridlineColor = Styles.CouleurGrille,
            .MinorGridlineStyle = LineStyle.None}

        ' Personnaliser les labels de l'axe X avec les noms de séries
        axeX.LabelFormatter = Function(v)
            Dim idx As Integer = CInt(Math.Round(v))
            If idx >= 0 AndAlso idx < seriesVisibles.Count Then
                Dim sg2 = seriesVisibles(idx)
                Return sg2.Nom & If(sg2.Unite <> "", Chr(10) & "(" & sg2.Unite & ")", "")
            End If
            Return ""
        End Function

        ' RectangleBarSeries pour des barres bien visibles
        Dim rbs As New OxyPlot.Series.RectangleBarSeries() With {
            .XAxisKey = axeX.Key, .YAxisKey = axeVal.Key}
        For i As Integer = 0 To seriesVisibles.Count - 1
            Dim sg = seriesVisibles(i)
            Dim pts = historique.ObtenirSerie(sg.Cle)
            Dim valeur As Double = 0
            If pts.Count > 0 Then
                Dim dernier = pts.LastOrDefault(Function(p) Not p.EnErreur AndAlso Not Double.IsNaN(p.Valeur))
                If dernier IsNot Nothing Then valeur = dernier.Valeur
            End If
            Dim couleur = Styles.ObtenirStyle(sg.Cle, sg.Couleur).Couleur
            ' Barre de i-0.35 à i+0.35, de 0 à valeur
            rbs.Items.Add(New OxyPlot.Series.RectangleBarItem(
                i - 0.35, 0, i + 0.35, valeur) With {.Color = couleur})
        Next
        _plotModel.Series.Add(rbs)

        axeX.Key   = "HistoX"
        axeVal.Key = "HistoY"
        _plotModel.Background         = Styles.CouleurFond
        _plotModel.PlotAreaBackground = Styles.CouleurFond
        _plotModel.TextColor          = Styles.CouleurTexte
        _plotModel.Axes.Add(axeX)
        _plotModel.Axes.Add(axeVal)
        _plotModel.InvalidatePlot(True)
    End Sub

        ''' <summary>
    ''' Exporte le graphique en PNG de manière silencieuse (sans boîte de dialogue).
    ''' Retourne True si succès.
    ''' </summary>
    Public Function ExporterPNGSilencieux(chemin As String,
                                          Optional largeur As Integer = 1400,
                                          Optional hauteur As Integer = 700) As Boolean
        Try
            Using stream = File.OpenWrite(chemin)
                Dim exp As New OxyPlot.WindowsForms.PngExporter() With {
                    .Width  = largeur,
                    .Height = hauteur
                }
                exp.Export(_plotModel, stream)
            End Using
            Return True
        Catch
            Return False
        End Try
    End Function

    Public Sub ExporterPNG(Optional chemin As String = "")
        If chemin = "" Then
            Using dlg As New SaveFileDialog() With {
                .Filter   = "PNG (*.png)|*.png",
                .FileName = "Graphique_" & DateTime.Now.ToString("yyyyMMdd-HH.mm.ss")
            }
                If dlg.ShowDialog() <> DialogResult.OK Then Return
                chemin = dlg.FileName
            End Using
        End If
        Try
            Using stream = File.OpenWrite(chemin)
                Dim exp As New OxyPlot.WindowsForms.PngExporter() With {
                    .Width  = Math.Max(Width, 1200),
                    .Height = Math.Max(Height, 600)
                }
                exp.Export(_plotModel, stream)
            End Using
            MessageBox.Show("Exporté : " & chemin, "PNG",
                MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("Erreur export : " & ex.Message, "Erreur",
                MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Public Sub ResetZoom()
        For Each axe In _plotModel.Axes
            axe.Reset()
        Next
        _plotModel.InvalidatePlot(False)
    End Sub

    ' ── Utilitaire ─────────────────────────────────────────────────────────────

    Private Shared Function ToOxy(c As Color) As OxyColor
        Return OxyColor.FromArgb(c.A, c.R, c.G, c.B)
    End Function

End Class
