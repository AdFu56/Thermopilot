Imports System
Imports System.Text.RegularExpressions

''' <summary>
''' Parse une durée avec unité optionnelle.
''' Syntaxes acceptées :
'''   "120"        → 120 secondes (défaut)
'''   "120[s]"     → 120 secondes
'''   "2[min]"     → 120 secondes
'''   "1.5[h]"     → 5400 secondes
'''   "2[j]"       → 172800 secondes
'''   "500[ms]"    → 0.5 secondes
''' Les séparateurs décimaux , et . sont tous deux acceptés.
''' </summary>
Public Module ParseurDuree

    ''' <summary>Convertit une chaîne durée+unité en secondes. Lève FormatException si invalide.</summary>
    Public Function EnSecondes(texte As String) As Double
        If texte Is Nothing OrElse texte.Trim() = "" Then
            Throw New FormatException("Durée vide.")
        End If
        Dim t = texte.Trim().Replace(",", ".")

        ' Chercher une unité entre crochets
        Dim m = Regex.Match(t, "^([0-9]+\.?[0-9]*(?:e[+-]?[0-9]+)?)\s*\[([^\]]+)\]$",
                            RegexOptions.IgnoreCase)
        If m.Success Then
            Dim valeur As Double
            If Not Double.TryParse(m.Groups(1).Value,
                    Globalization.NumberStyles.Float,
                    Globalization.CultureInfo.InvariantCulture, valeur) Then
                Throw New FormatException("Valeur numérique invalide : " & m.Groups(1).Value)
            End If
            Dim unite = m.Groups(2).Value.Trim().ToLower()
            Select Case unite
                Case "ms"           : Return valeur / 1000.0
                Case "s", "sec"     : Return valeur
                Case "min", "m"     : Return valeur * 60.0
                Case "h", "heure", "heures" : Return valeur * 3600.0
                Case "j", "jour", "jours", "d", "day" : Return valeur * 86400.0
                Case Else
                    Throw New FormatException("Unité inconnue : [" & unite & "]. Utilisez [ms], [s], [min], [h] ou [j].")
            End Select
        End If

        ' Pas d'unité → secondes par défaut
        Dim sec As Double
        If Double.TryParse(t, Globalization.NumberStyles.Float,
                Globalization.CultureInfo.InvariantCulture, sec) Then
            Return sec
        End If
        Throw New FormatException("Format invalide : '" & texte & "'. Exemples : 120, 2[min], 1.5[h], 6[j]")
    End Function

    ''' <summary>Retourne True si le texte est une durée valide.</summary>
    Public Function EstValide(texte As String) As Boolean
        Try
            EnSecondes(texte)
            Return True
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Formate une durée en secondes avec l'unité la plus lisible.
    ''' Ex: 90 → "1.5[min]", 7200 → "2[h]"
    ''' </summary>
    Public Function FormatAuto(secondes As Double) As String
        If secondes >= 86400 AndAlso secondes Mod 86400 = 0 Then
            Return CInt(secondes / 86400).ToString() & "[j]"
        ElseIf secondes >= 3600 AndAlso secondes Mod 3600 = 0 Then
            Return CInt(secondes / 3600).ToString() & "[h]"
        ElseIf secondes >= 60 AndAlso secondes Mod 60 = 0 Then
            Return CInt(secondes / 60).ToString() & "[min]"
        ElseIf secondes < 1 Then
            Return CInt(secondes * 1000).ToString() & "[ms]"
        Else
            Return secondes.ToString("G", Globalization.CultureInfo.InvariantCulture) & "[s]"
        End If
    End Function

    ''' <summary>Libellé de l'unité choisie dans une ComboBox d'unités.</summary>
    Public Function LibelleUnites() As String()
        Return {"[s] — secondes", "[min] — minutes", "[h] — heures", "[j] — jours", "[ms] — millisecondes"}
    End Function

    ''' <summary>Suffixe correspondant à l'index de la ComboBox d'unités.</summary>
    Public Function SuffixeParIndex(idx As Integer) As String
        Select Case idx
            Case 0 : Return "[s]"
            Case 1 : Return "[min]"
            Case 2 : Return "[h]"
            Case 3 : Return "[j]"
            Case 4 : Return "[ms]"
            Case Else : Return "[s]"
        End Select
    End Function

End Module
