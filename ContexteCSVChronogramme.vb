Imports System.Collections.Generic

''' <summary>
''' Données du chronogramme actif transmises au MoteurAcquisition
''' pour être écrites en commentaire dans le fichier CSV.
''' </summary>
Public Class ContexteCSVChronogramme
    Public Property DureeTotale    As String = ""
    Public Property Boucler        As Boolean = True
    Public Property ArreterAcqFin  As Boolean = False
    Public Property Etapes         As New List(Of String)
    Public Property Regles         As New List(Of String)
End Class
