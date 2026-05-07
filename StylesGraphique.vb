Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports Microsoft.VisualBasic
Imports OxyPlot
Imports OxyPlot.Axes
Imports OxyPlot.Legends
Imports OxyPlot.Series

' ═══════════════════════════════════════════════════════════════════════════════
'  STYLE SÉRIE — préférences visuelles d'une courbe individuelle
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Préférences visuelles d'une série individuelle du graphique.
''' Persisté dans config.ini via StylesGraphique.
''' </summary>
Public Class StyleSerie
    Public Property Couleur        As OxyColor = OxyColors.White
    Public Property StyleLigne     As OxyPlot.LineStyle = OxyPlot.LineStyle.Solid
    Public Property Marqueur       As MarkerType = MarkerType.None
    Public Property Epaisseur      As Double = 1.8
    Public Property TailleMarqueur As Double = 4.0

    Public Function Clone() As StyleSerie
        Return New StyleSerie() With {
            .Couleur        = Me.Couleur,
            .StyleLigne     = Me.StyleLigne,
            .Marqueur       = Me.Marqueur,
            .Epaisseur      = Me.Epaisseur,
            .TailleMarqueur = Me.TailleMarqueur
        }
    End Function
End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  STYLES GRAPHIQUE — conteneur de toutes les préférences visuelles
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Conteneur de tous les styles du graphique :
''' fond, texte, grille, légende, et un StyleSerie par voie.
''' Chargement/sauvegarde dans config.ini.
''' </summary>
Public Class StylesGraphique

    ' ─── Styles globaux ───────────────────────────────────────────────────────

    Public Property CouleurFond   As OxyColor = OxyColor.FromRgb(18, 20, 32)
    Public Property CouleurTexte  As OxyColor = OxyColor.FromRgb(180, 190, 210)
    Public Property CouleurGrille As OxyColor = OxyColor.FromArgb(80, 40, 44, 60)
    Public Property StyleGrille   As OxyPlot.LineStyle = OxyPlot.LineStyle.Dot

    ' ─── Styles légende ───────────────────────────────────────────────────────

    Public Property CouleurFondLegende    As OxyColor = OxyColor.FromArgb(200, 30, 32, 45)
    Public Property CouleurTexteLegende   As OxyColor = OxyColor.FromRgb(180, 190, 210)
    Public Property CouleurBordureLegende As OxyColor = OxyColor.FromRgb(60, 65, 80)
    Public Property TaillePoliceLegende   As Double   = 9.0
    Public Property PositionLegende       As OxyPlot.Legends.LegendPosition  = OxyPlot.Legends.LegendPosition.TopRight
    Public Property PlacementLegende      As OxyPlot.Legends.LegendPlacement = OxyPlot.Legends.LegendPlacement.Inside
    Public Property LegendeVisible        As Boolean = True
    Public Property MargeLegende          As Double  = 10.0
    Public Property PaddingLegende        As Double  = 6.0

    ' ─── Styles axes ──────────────────────────────────────────────────────────

    Public Property TaillePoliceAxes      As Double  = 8.0   ' police des valeurs (graduations)
    Public Property TaillePoliceAxesTitre As Double  = 8.0   ' police du titre de l'axe (unité)
    Public Property MargeBasse            As Double  = 40.0  ' px entre axe X et bord bas de la fenêtre

    ' ─── Styles par série ─────────────────────────────────────────────────────

    Private _styles As New Dictionary(Of String, StyleSerie)

    ' Palettes par défaut synchronisées avec PanelGraphique
    Private Shared ReadOnly _paletteDefaut As OxyColor() = {
        OxyColor.FromRgb(220, 80,  50),  OxyColor.FromRgb(30,  130, 210),
        OxyColor.FromRgb(50,  185, 80),  OxyColor.FromRgb(220, 155, 20),
        OxyColor.FromRgb(155, 60,  215), OxyColor.FromRgb(20,  195, 185),
        OxyColor.FromRgb(215, 65,  155), OxyColor.FromRgb(110, 110, 110),
        OxyColor.FromRgb(175, 95,  20),  OxyColor.FromRgb(85,  175, 215),
        OxyColor.FromRgb(185, 225, 80),  OxyColor.FromRgb(215, 105, 105),
        OxyColor.FromRgb(105, 215, 155), OxyColor.FromRgb(105, 105, 215),
        OxyColor.FromRgb(215, 175, 55),  OxyColor.FromRgb(55,  215, 215)
    }
    Private Shared ReadOnly _paletteBinaireDefaut As OxyColor() = {
        OxyColor.FromRgb(255, 140, 0),   OxyColor.FromRgb(200, 60,  200),
        OxyColor.FromRgb(0,   180, 120), OxyColor.FromRgb(180, 50,  50),
        OxyColor.FromRgb(50,  50,  200)
    }

    ' ─── Accès ────────────────────────────────────────────────────────────────

    Public Function ObtenirStyle(cle As String, couleurDefaut As Color) As StyleSerie
        If Not _styles.ContainsKey(cle) Then
            _styles(cle) = New StyleSerie() With {
                .Couleur = OxyColor.FromArgb(couleurDefaut.A,
                                             couleurDefaut.R,
                                             couleurDefaut.G,
                                             couleurDefaut.B)
            }
        End If
        Return _styles(cle)
    End Function

    Public Function ObtenirCouleur(cle As String, couleurDefaut As Color) As OxyColor
        Return ObtenirStyle(cle, couleurDefaut).Couleur
    End Function

    Public Sub DefinirStyle(cle As String, style As StyleSerie)
        _styles(cle) = style
    End Sub

    ' ─── Reset ────────────────────────────────────────────────────────────────

    Public Sub ResetTout(series As List(Of PanelGraphique.SerieGraphique))
        CouleurFond           = OxyColor.FromRgb(18, 20, 32)
        CouleurTexte          = OxyColor.FromRgb(180, 190, 210)
        CouleurGrille         = OxyColor.FromArgb(80, 40, 44, 60)
        StyleGrille           = OxyPlot.LineStyle.Dot
        CouleurFondLegende    = OxyColor.FromArgb(200, 30, 32, 45)
        CouleurTexteLegende   = OxyColor.FromRgb(180, 190, 210)
        CouleurBordureLegende = OxyColor.FromRgb(60, 65, 80)
        TaillePoliceLegende   = 9.0
        PositionLegende       = OxyPlot.Legends.LegendPosition.TopRight
        PlacementLegende      = OxyPlot.Legends.LegendPlacement.Inside
        LegendeVisible        = True
        MargeLegende          = 10.0
        PaddingLegende        = 6.0
        TaillePoliceAxes      = 8.0
        TaillePoliceAxesTitre = 8.0
        MargeBasse            = 40.0
        _styles.Clear()

        Dim idxAnal = 0 : Dim idxBin = 0
        For Each sg In series
            Dim coulDef = If(sg.EstBinaire,
                _paletteBinaireDefaut(idxBin Mod _paletteBinaireDefaut.Length),
                _paletteDefaut(idxAnal Mod _paletteDefaut.Length))
            _styles(sg.Cle) = New StyleSerie() With {.Couleur = coulDef}
            If sg.EstBinaire Then idxBin += 1 Else idxAnal += 1
        Next
    End Sub

    ' ─── Clone / Restaurer ────────────────────────────────────────────────────

    Public Function Clone() As StylesGraphique
        Dim r As New StylesGraphique()
        r.CouleurFond          = Me.CouleurFond
        r.CouleurTexte         = Me.CouleurTexte
        r.CouleurGrille        = Me.CouleurGrille
        r.StyleGrille          = Me.StyleGrille
        r.CouleurFondLegende    = Me.CouleurFondLegende
        r.CouleurTexteLegende   = Me.CouleurTexteLegende
        r.CouleurBordureLegende = Me.CouleurBordureLegende
        r.TaillePoliceLegende   = Me.TaillePoliceLegende
        r.PositionLegende       = Me.PositionLegende
        r.PlacementLegende      = Me.PlacementLegende
        r.LegendeVisible        = Me.LegendeVisible
        r.MargeLegende          = Me.MargeLegende
        r.PaddingLegende        = Me.PaddingLegende
        r.TaillePoliceAxes      = Me.TaillePoliceAxes
        r.TaillePoliceAxesTitre = Me.TaillePoliceAxesTitre
        r.MargeBasse            = Me.MargeBasse
        For Each kvp In _styles
            r._styles(kvp.Key) = kvp.Value.Clone()
        Next
        Return r
    End Function

    Public Sub RestaurerDepuis(source As StylesGraphique)
        Me.CouleurFond          = source.CouleurFond
        Me.CouleurTexte         = source.CouleurTexte
        Me.CouleurGrille        = source.CouleurGrille
        Me.StyleGrille          = source.StyleGrille
        Me.CouleurFondLegende    = source.CouleurFondLegende
        Me.CouleurTexteLegende   = source.CouleurTexteLegende
        Me.CouleurBordureLegende = source.CouleurBordureLegende
        Me.TaillePoliceLegende   = source.TaillePoliceLegende
        Me.PositionLegende       = source.PositionLegende
        Me.PlacementLegende      = source.PlacementLegende
        Me.LegendeVisible        = source.LegendeVisible
        Me.MargeLegende          = source.MargeLegende
        Me.PaddingLegende        = source.PaddingLegende
        Me.TaillePoliceAxes      = source.TaillePoliceAxes
        Me.TaillePoliceAxesTitre = source.TaillePoliceAxesTitre
        Me.MargeBasse            = source.MargeBasse
        _styles.Clear()
        For Each kvp In source._styles
            _styles(kvp.Key) = kvp.Value.Clone()
        Next
    End Sub

    ' ─── Persistance ──────────────────────────────────────────────────────────

    Private Const SEC As String = "StylesGraphique"

    ''' <summary>Sauvegarde dans une section explicite (pour les onglets Résultats).</summary>
    Public Sub SauverDansConfig(cfg As ConfigManager, section As String)
        Dim ancSEC = SEC
        ' Utiliser la section fournie en remplaçant temporairement via un ConfigManager proxy
        cfg.Set_(section, "CouleurFond",           OxyColorVersHex(CouleurFond))
        cfg.Set_(section, "CouleurTexte",          OxyColorVersHex(CouleurTexte))
        cfg.Set_(section, "CouleurGrille",         OxyColorVersHex(CouleurGrille))
        cfg.Set_(section, "StyleGrille",           CInt(StyleGrille))
        cfg.Set_(section, "CouleurFondLegende",    OxyColorVersHex(CouleurFondLegende))
        cfg.Set_(section, "CouleurTexteLegende",   OxyColorVersHex(CouleurTexteLegende))
        cfg.Set_(section, "CouleurBordureLegende", OxyColorVersHex(CouleurBordureLegende))
        cfg.Set_(section, "TaillePoliceLegende",   TaillePoliceLegende.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(section, "PositionLegende",       CInt(PositionLegende))
        cfg.Set_(section, "PlacementLegende",      CInt(PlacementLegende))
        cfg.Set_(section, "LegendeVisible",        LegendeVisible)
        cfg.Set_(section, "MargeLegende",          MargeLegende.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(section, "TaillePoliceAxes",      TaillePoliceAxes.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(section, "MargeBasse",            MargeBasse.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(section, "TaillePoliceAxesTitre", TaillePoliceAxesTitre.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(section, "PaddingLegende",        PaddingLegende.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        For Each kvp In _styles
            Dim pref = Uri.EscapeDataString(kvp.Key)
            cfg.Set_(section, pref & "_Coul",  OxyColorVersHex(kvp.Value.Couleur))
            cfg.Set_(section, pref & "_Style", CInt(kvp.Value.StyleLigne))
            cfg.Set_(section, pref & "_Marq",  CInt(kvp.Value.Marqueur))
            cfg.Set_(section, pref & "_Epais", kvp.Value.Epaisseur.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
            cfg.Set_(section, pref & "_TMarq", kvp.Value.TailleMarqueur.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        Next
    End Sub

    ''' <summary>Charge depuis une section explicite (pour les onglets Résultats).</summary>
    Public Sub ChargerDepuisConfig(cfg As ConfigManager, section As String)
        CouleurFond           = HexVersOxyColor(cfg.Get_(section, "CouleurFond",           OxyColorVersHex(CouleurFond)))
        CouleurTexte          = HexVersOxyColor(cfg.Get_(section, "CouleurTexte",          OxyColorVersHex(CouleurTexte)))
        CouleurGrille         = HexVersOxyColor(cfg.Get_(section, "CouleurGrille",         OxyColorVersHex(CouleurGrille)))
        StyleGrille           = CType(cfg.GetInt(section, "StyleGrille", CInt(StyleGrille)), OxyPlot.LineStyle)
        CouleurFondLegende    = HexVersOxyColor(cfg.Get_(section, "CouleurFondLegende",    OxyColorVersHex(CouleurFondLegende)))
        CouleurTexteLegende   = HexVersOxyColor(cfg.Get_(section, "CouleurTexteLegende",   OxyColorVersHex(CouleurTexteLegende)))
        CouleurBordureLegende = HexVersOxyColor(cfg.Get_(section, "CouleurBordureLegende", OxyColorVersHex(CouleurBordureLegende)))
        TaillePoliceLegende   = cfg.GetDouble(section, "TaillePoliceLegende", TaillePoliceLegende)
        PositionLegende       = CType(cfg.GetInt(section, "PositionLegende",  CInt(PositionLegende)),  OxyPlot.Legends.LegendPosition)
        PlacementLegende      = CType(cfg.GetInt(section, "PlacementLegende", CInt(PlacementLegende)), OxyPlot.Legends.LegendPlacement)
        LegendeVisible        = cfg.GetBool(section, "LegendeVisible", True)
        MargeLegende          = cfg.GetDouble(section, "MargeLegende",  10.0)
        PaddingLegende        = cfg.GetDouble(section, "PaddingLegende", 6.0)
        TaillePoliceAxes      = cfg.GetDouble(section, "TaillePoliceAxes",      8.0)
        TaillePoliceAxesTitre = cfg.GetDouble(section, "TaillePoliceAxesTitre", 8.0)
        MargeBasse            = cfg.GetDouble(section, "MargeBasse",             40.0)
        _styles.Clear()
    End Sub

    Public Sub SauverDansConfig(cfg As ConfigManager)
        cfg.Set_(SEC, "CouleurFond",           OxyColorVersHex(CouleurFond))
        cfg.Set_(SEC, "CouleurTexte",          OxyColorVersHex(CouleurTexte))
        cfg.Set_(SEC, "CouleurGrille",         OxyColorVersHex(CouleurGrille))
        cfg.Set_(SEC, "StyleGrille",           CInt(StyleGrille))
        cfg.Set_(SEC, "CouleurFondLegende",    OxyColorVersHex(CouleurFondLegende))
        cfg.Set_(SEC, "CouleurTexteLegende",   OxyColorVersHex(CouleurTexteLegende))
        cfg.Set_(SEC, "CouleurBordureLegende", OxyColorVersHex(CouleurBordureLegende))
        cfg.Set_(SEC, "TaillePoliceLegende",   TaillePoliceLegende.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(SEC, "PositionLegende",       CInt(PositionLegende))
        cfg.Set_(SEC, "PlacementLegende",      CInt(PlacementLegende))
        cfg.Set_(SEC, "LegendeVisible",        LegendeVisible)
        cfg.Set_(SEC, "MargeLegende",          MargeLegende.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(SEC, "TaillePoliceAxes",      TaillePoliceAxes.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(SEC, "MargeBasse",             MargeBasse.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(SEC, "TaillePoliceAxesTitre", TaillePoliceAxesTitre.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        cfg.Set_(SEC, "PaddingLegende",        PaddingLegende.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))

        For Each kvp In _styles
            Dim pref = Uri.EscapeDataString(kvp.Key)
            cfg.Set_(SEC, pref & "_Coul",  OxyColorVersHex(kvp.Value.Couleur))
            cfg.Set_(SEC, pref & "_Style", CInt(kvp.Value.StyleLigne))
            cfg.Set_(SEC, pref & "_Marq",  CInt(kvp.Value.Marqueur))
            cfg.Set_(SEC, pref & "_Epais", kvp.Value.Epaisseur.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
            cfg.Set_(SEC, pref & "_TMarq", kvp.Value.TailleMarqueur.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
        Next
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager)
        CouleurFond           = HexVersOxyColor(cfg.Get_(SEC, "CouleurFond",           OxyColorVersHex(CouleurFond)))
        CouleurTexte          = HexVersOxyColor(cfg.Get_(SEC, "CouleurTexte",          OxyColorVersHex(CouleurTexte)))
        CouleurGrille         = HexVersOxyColor(cfg.Get_(SEC, "CouleurGrille",         OxyColorVersHex(CouleurGrille)))
        StyleGrille           = CType(cfg.GetInt(SEC, "StyleGrille", CInt(StyleGrille)), OxyPlot.LineStyle)
        CouleurFondLegende    = HexVersOxyColor(cfg.Get_(SEC, "CouleurFondLegende",    OxyColorVersHex(CouleurFondLegende)))
        CouleurTexteLegende   = HexVersOxyColor(cfg.Get_(SEC, "CouleurTexteLegende",   OxyColorVersHex(CouleurTexteLegende)))
        CouleurBordureLegende = HexVersOxyColor(cfg.Get_(SEC, "CouleurBordureLegende", OxyColorVersHex(CouleurBordureLegende)))
        TaillePoliceLegende   = cfg.GetDouble(SEC, "TaillePoliceLegende", TaillePoliceLegende)
        PositionLegende       = CType(cfg.GetInt(SEC, "PositionLegende",  CInt(PositionLegende)),  OxyPlot.Legends.LegendPosition)
        PlacementLegende      = CType(cfg.GetInt(SEC, "PlacementLegende", CInt(PlacementLegende)), OxyPlot.Legends.LegendPlacement)
        LegendeVisible        = cfg.GetBool(SEC, "LegendeVisible", True)
        MargeLegende          = cfg.GetDouble(SEC, "MargeLegende",  10.0)
        PaddingLegende        = cfg.GetDouble(SEC, "PaddingLegende", 6.0)
        TaillePoliceAxes      = cfg.GetDouble(SEC, "TaillePoliceAxes",      8.0)
        TaillePoliceAxesTitre = cfg.GetDouble(SEC, "TaillePoliceAxesTitre", 8.0)
        MargeBasse            = cfg.GetDouble(SEC, "MargeBasse",             40.0)
        ' Les styles par série sont chargés à la demande via ChargerStyleSerie
        ' (appelé dans PanelGraphique.DefinirSeries quand les clés sont connues)
        _styles.Clear()
    End Sub

    ''' <summary>
    ''' Charge le style d'une série depuis config.ini.
    ''' Appelé dans PanelGraphique.DefinirSeries pour chaque série.
    ''' </summary>
    Public Sub ChargerStyleSerie(cfg As ConfigManager, cle As String, couleurDefaut As OxyColor)
        ChargerStyleSerie(cfg, SEC, cle, couleurDefaut)
    End Sub

    ''' <summary>Charge le style d'une série depuis une section explicite (onglets Résultats).</summary>
    Public Sub ChargerStyleSerie(cfg As ConfigManager, section As String, cle As String, couleurDefaut As OxyColor)
        Dim pref    = Uri.EscapeDataString(cle)
        Dim coulHex = cfg.Get_(section, pref & "_Coul", "")
        If String.IsNullOrEmpty(coulHex) Then Return  ' pas de style sauvegardé → couleur par défaut conservée

        Dim style As New StyleSerie()
        style.Couleur        = HexVersOxyColor(coulHex)
        style.StyleLigne     = CType(cfg.GetInt(section, pref & "_Style", CInt(OxyPlot.LineStyle.Solid)), OxyPlot.LineStyle)
        style.Marqueur       = CType(cfg.GetInt(section, pref & "_Marq",  CInt(MarkerType.None)), MarkerType)
        style.Epaisseur      = cfg.GetDouble(section, pref & "_Epais", 1.8)
        style.TailleMarqueur = cfg.GetDouble(section, pref & "_TMarq", 4.0)
        _styles(cle)         = style
    End Sub

    ' ─── Utilitaires couleur ──────────────────────────────────────────────────

    Public Shared Function OxyColorVersHex(c As OxyColor) As String
        Return String.Format("{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B)
    End Function

    Public Shared Function HexVersOxyColor(hex As String) As OxyColor
        Try
            If hex.Length = 8 Then
                Return OxyColor.FromArgb(
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16),
                    Convert.ToByte(hex.Substring(6, 2), 16))
            ElseIf hex.Length = 6 Then
                Return OxyColor.FromRgb(
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16))
            End If
        Catch
        End Try
        Return OxyColors.White
    End Function

End Class
