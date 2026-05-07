#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
generer_rapport.py — Thermopilot v2.0
Génère un rapport PDF de configuration d'essai.
Appelé par VB.NET via Process.Start avec un fichier JSON en argument.
Usage : python generer_rapport.py <chemin_json> <chemin_pdf_sortie>
"""

import sys
import json
import os
from datetime import datetime

from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER, TA_RIGHT
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    HRFlowable, PageBreak, KeepTogether
)
from reportlab.platypus.flowables import Flowable

# ─── Palette couleurs ─────────────────────────────────────────────────────────
C_BLEU_FONCE  = colors.HexColor("#1A2A4A")
C_BLEU_MOYEN  = colors.HexColor("#2A5BA8")
C_BLEU_CLAIR  = colors.HexColor("#D0E4F7")
C_GRIS_CLAIR  = colors.HexColor("#F4F6F9")
C_GRIS_LIGNE  = colors.HexColor("#E0E4EA")
C_VERT        = colors.HexColor("#1A6B3A")
C_VERT_CLAIR  = colors.HexColor("#D4EDDA")
C_ORANGE      = colors.HexColor("#C86000")
C_ORANGE_CLAIR= colors.HexColor("#FFF0DC")
C_ROUGE_CLAIR = colors.HexColor("#FAE0E0")
C_BLANC       = colors.white
C_NOIR        = colors.black

W = A4[0]  # largeur utile

# ─── Styles ───────────────────────────────────────────────────────────────────
def creer_styles(donnees):
    base = getSampleStyleSheet()
    styles = {}

    police = donnees.get('police', 'Helvetica')
    if police not in ('Helvetica', 'Times-Roman', 'Courier'):
        police = 'Helvetica'
    police_bold   = police + '-Bold'
    police_italic = police + '-Oblique' if police == 'Helvetica' else police + '-Italic'

    styles['titre_doc'] = ParagraphStyle('titre_doc',
        fontName=police_bold, fontSize=22,
        textColor=C_BLANC, alignment=TA_CENTER, leading=28)

    styles['sous_titre'] = ParagraphStyle('sous_titre',
        fontName='Helvetica', fontSize=11,
        textColor=C_BLEU_CLAIR, alignment=TA_CENTER, leading=16)

    styles['h1'] = ParagraphStyle('h1',
        fontName=police_bold, fontSize=13,
        textColor=C_BLANC, alignment=TA_LEFT,
        spaceBefore=14, spaceAfter=4, leading=16)

    styles['h2'] = ParagraphStyle('h2',
        fontName=police_bold, fontSize=10,
        textColor=C_BLEU_FONCE, alignment=TA_LEFT,
        spaceBefore=8, spaceAfter=3)

    styles['corps'] = ParagraphStyle('corps',
        fontName=police, fontSize=9,
        textColor=C_NOIR, leading=13, spaceBefore=2)

    styles['mono'] = ParagraphStyle('mono',
        fontName='Courier', fontSize=8,
        textColor=C_BLEU_FONCE, leading=12)

    styles['label'] = ParagraphStyle('label',
        fontName=police_bold, fontSize=8,
        textColor=C_BLEU_FONCE)

    styles['valeur'] = ParagraphStyle('valeur',
        fontName=police, fontSize=8,
        textColor=C_NOIR)

    styles['note'] = ParagraphStyle('note',
        fontName=police_italic, fontSize=8,
        textColor=colors.HexColor("#666666"), spaceBefore=2)

    return styles


# ─── Helpers layout ───────────────────────────────────────────────────────────

class BandeauSection(Flowable):
    """Bandeau coloré avec titre de section."""
    def __init__(self, titre, couleur=C_BLEU_MOYEN, largeur=None, hauteur=8*mm):
        super().__init__()
        self.titre    = titre
        self.couleur  = couleur
        self.largeur  = largeur
        self.hauteur  = hauteur

    def wrap(self, aw, ah):
        self.largeur = self.largeur or aw
        return self.largeur, self.hauteur + 6*mm

    def draw(self):
        c = self.canv
        # Fond
        c.setFillColor(self.couleur)
        c.roundRect(0, 3*mm, self.largeur, self.hauteur, 2*mm, stroke=0, fill=1)
        # Texte
        c.setFillColor(C_BLANC)
        c.setFont('Helvetica-Bold', 11)
        c.drawString(6*mm, 3*mm + (self.hauteur - 4*mm)/2, self.titre)


def tableau(data, col_widths, style_extra=None, alt=True):
    """Table avec style standard."""
    style = [
        ('BACKGROUND', (0, 0), (-1, 0), C_BLEU_FONCE),
        ('TEXTCOLOR',  (0, 0), (-1, 0), C_BLANC),
        ('FONTNAME',   (0, 0), (-1, 0), 'Helvetica-Bold'),
        ('FONTSIZE',   (0, 0), (-1, 0), 8),
        ('FONTNAME',   (0, 1), (-1, -1), 'Helvetica'),
        ('FONTSIZE',   (0, 1), (-1, -1), 8),
        ('ROWBACKGROUND', (0, 1), (-1, -1), [C_BLANC, C_GRIS_CLAIR]) if alt else ('',),
        ('GRID',       (0, 0), (-1, -1), 0.4, C_GRIS_LIGNE),
        ('VALIGN',     (0, 0), (-1, -1), 'MIDDLE'),
        ('TOPPADDING', (0, 0), (-1, -1), 3),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 3),
        ('LEFTPADDING', (0, 0), (-1, -1), 5),
    ]
    if alt:
        for i in range(1, len(data)):
            bg = C_GRIS_CLAIR if i % 2 == 0 else C_BLANC
            style.append(('BACKGROUND', (0, i), (-1, i), bg))
    if style_extra:
        style.extend(style_extra)
    t = Table(data, colWidths=col_widths)
    t.setStyle(TableStyle(style))
    return t


def kv_bloc(paires, styles, largeur=150*mm):
    """Bloc clé-valeur en deux colonnes."""
    data = []
    for k, v in paires:
        data.append([
            Paragraph(str(k), styles['label']),
            Paragraph(str(v) if v is not None else "—", styles['valeur'])
        ])
    t = Table(data, colWidths=[55*mm, largeur - 55*mm])
    t.setStyle(TableStyle([
        ('VALIGN',       (0, 0), (-1, -1), 'TOP'),
        ('TOPPADDING',   (0, 0), (-1, -1), 2),
        ('BOTTOMPADDING',(0, 0), (-1, -1), 2),
        ('LEFTPADDING',  (0, 0), (-1, -1), 3),
    ]))
    return t


# ─── Entête / pied de page ────────────────────────────────────────────────────

def on_page(canvas, doc, donnees):
    canvas.saveState()
    W_, H = A4
    marge = 15*mm

    # Pied de page
    canvas.setFillColor(C_BLEU_FONCE)
    canvas.rect(marge, 8*mm, W_ - 2*marge, 0.3*mm, fill=1, stroke=0)
    canvas.setFont('Helvetica', 7)
    canvas.setFillColor(colors.HexColor("#888888"))
    canvas.drawString(marge, 5*mm,
        f"Thermopilot v2.0 — IRDL PTR4 — {donnees.get('date_generation','')}")
    canvas.drawRightString(W_ - marge, 5*mm, f"Page {doc.page}")

    canvas.restoreState()


# ─── Génération ───────────────────────────────────────────────────────────────

def generer(donnees, chemin_pdf):
    styles = creer_styles(donnees)
    marge  = 15*mm

    doc = SimpleDocTemplate(
        chemin_pdf,
        pagesize    = A4,
        leftMargin  = marge,
        rightMargin = marge,
        topMargin   = marge,
        bottomMargin= 20*mm,
        title       = "Rapport d'essai Thermopilot",
        author      = "Thermopilot v2.0 — IRDL PTR4"
    )

    story = []
    LU = W - 2*marge   # largeur utile

    # ── PAGE DE COUVERTURE ───────────────────────────────────────────────────
    story.append(Spacer(1, 25*mm))

    # Bandeau titre
    titre_data = [[Paragraph("THERMOPILOT v2.0", styles['titre_doc'])]]
    t_titre = Table(titre_data, colWidths=[LU])
    t_titre.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), C_BLEU_FONCE),
        ('TOPPADDING', (0,0), (-1,-1), 10),
        ('BOTTOMPADDING', (0,0), (-1,-1), 10),
        ('ROUNDEDCORNERS', [3*mm]),
    ]))
    story.append(t_titre)
    story.append(Spacer(1, 4*mm))

    t_st = Table([[Paragraph("Rapport de configuration d'essai", styles['sous_titre'])]],
                 colWidths=[LU])
    t_st.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), C_BLEU_MOYEN),
        ('TOPPADDING', (0,0), (-1,-1), 6),
        ('BOTTOMPADDING', (0,0), (-1,-1), 6),
    ]))
    story.append(t_st)
    story.append(Spacer(1, 12*mm))

    # Infos essai
    # Logo
    chemin_logo = donnees.get('chemin_logo', '')
    if chemin_logo and os.path.exists(chemin_logo):
        from reportlab.platypus import Image as RLImage
        try:
            logo_w, logo_h = 50*mm, 25*mm
            img = RLImage(chemin_logo, width=logo_w, height=logo_h)
            img.hAlign = 'RIGHT'
            story.append(img)
            story.append(Spacer(1, 2*mm))
        except Exception:
            pass

    infos_essai = [
        ("Projet / Essai",             donnees.get('projet', '—')),
        ("Date et heure de démarrage", donnees.get('date_debut', '—')),
        ("Fichier CSV de mesures",     donnees.get('chemin_csv', '—')),
        ("Opérateur",                  donnees.get('operateur', '—')),
        ("Laboratoire",                donnees.get('labo', '—')),
        ("Mode",                       donnees.get('mode', '—')),
    ]
    story.append(kv_bloc(infos_essai, styles, LU))
    notes = donnees.get('notes', '').strip()
    if notes:
        story.append(Spacer(1, 3*mm))
        story.append(Paragraph("Notes :", styles['h2']))
        story.append(Paragraph(notes.replace("\n", "<br/>"), styles['corps']))
    story.append(Spacer(1, 6*mm))
    story.append(HRFlowable(width=LU, color=C_BLEU_MOYEN, thickness=1))
    story.append(Spacer(1, 3*mm))
    story.append(Paragraph(
        f"Généré automatiquement le {donnees.get('date_generation','')}",
        styles['note']))
    story.append(PageBreak())

    # ── 1. CONNEXION ─────────────────────────────────────────────────────────
    story.append(BandeauSection("1. Connexion aux centrales"))
    story.append(Spacer(1, 3*mm))

    centrales = donnees.get('centrales', [])
    if centrales:
        entetes = [["Centrale", "Adresse IP", "Port", "Statut", "Modèle"]]
        for c in centrales:
            entetes.append([
                c.get('nom',''),
                c.get('ip',''),
                str(c.get('port', '')),
                c.get('statut',''),
                c.get('modele','')
            ])
        story.append(tableau(entetes, [30*mm, 40*mm, 20*mm, 30*mm, LU-120*mm]))
    else:
        story.append(Paragraph("Aucune centrale configurée.", styles['note']))
    story.append(Spacer(1, 5*mm))

    # ── 2. VOIES DE MESURE ───────────────────────────────────────────────────
    story.append(BandeauSection("2. Voies de mesure"))
    story.append(Spacer(1, 3*mm))

    voies = donnees.get('voies', [])
    if voies:
        entetes = [["Centrale", "N° voie", "Nom", "Unité", "Type",
                    "Alarme basse", "Alarme haute", "Voie surveillée"]]
        for v in voies:
            entetes.append([
                v.get('centrale',''),
                str(v.get('numero','')),
                v.get('nom',''),
                v.get('unite',''),
                v.get('type',''),
                str(v.get('alarme_basse','—')),
                str(v.get('alarme_haute','—')),
                "Oui" if v.get('secu_debit') == "Oui" else ""
            ])
        story.append(tableau(entetes,
            [25*mm, 17*mm, 35*mm, 15*mm, 25*mm, 18*mm, 18*mm, LU-153*mm]))
    else:
        story.append(Paragraph("Aucune voie active.", styles['note']))
    story.append(Spacer(1, 5*mm))

    # ── 3. SORTIES ANALOGIQUES ───────────────────────────────────────────────
    story.append(BandeauSection("3. Sorties analogiques / relais"))
    story.append(Spacer(1, 3*mm))

    sorties = donnees.get('sorties', [])
    if sorties:
        entetes = [["Centrale", "N° sortie", "Nom", "Mode", "Amplitude (V)",
                    "Arrêt si sécu."]]
        for s in sorties:
            entetes.append([
                s.get('centrale',''),
                str(s.get('numero','')),
                s.get('nom',''),
                s.get('mode',''),
                str(s.get('amplitude','')),
                "Oui" if s.get('secu_debit') == "Oui" else ""
            ])
        story.append(tableau(entetes,
            [25*mm, 20*mm, 40*mm, 35*mm, 25*mm, LU-145*mm]))
    else:
        story.append(Paragraph("Aucune sortie active.", styles['note']))
    story.append(Spacer(1, 5*mm))

    # ── 4. PARAMÈTRES D'ACQUISITION ──────────────────────────────────────────
    story.append(BandeauSection("4. Paramètres d'acquisition"))
    story.append(Spacer(1, 3*mm))

    acq = donnees.get('acquisition', {})
    paires_acq = [
        ("Intervalle d'acquisition",  acq.get('intervalle', '—')),
        ("Fichier CSV",               acq.get('chemin_csv', '—')),
        ("Format des valeurs",        acq.get('format_valeur', '—')),
        ("Unité de durée",            acq.get('unite_duree', '—')),
        ("Mode simulation",           "Oui" if acq.get('simulation') else "Non"),
    ]
    story.append(kv_bloc(paires_acq, styles, LU))
    story.append(Spacer(1, 5*mm))

    # ── 5. VOIES CALCULÉES ───────────────────────────────────────────────────
    calcs = donnees.get('calculs', [])
    if calcs:
        story.append(BandeauSection("5. Variables calculées utilisateur"))
        story.append(Spacer(1, 3*mm))
        entetes = [["Nom", "Unité", "Expression", "N moy"]]
        for vc in calcs:
            expr = vc.get('expression','')
            # Remplacer les clés par les noms de voies si disponible
            noms_voies = donnees.get('noms_voies_map', {})
            for cle, nom in noms_voies.items():
                expr = expr.replace('{' + cle + '}', nom)
            entetes.append([
                Paragraph(vc.get('nom',''), styles['corps']),
                Paragraph(vc.get('unite',''), styles['corps']),
                Paragraph(expr, styles['mono']),
                str(vc.get('nb_moy', 1))
            ])
        story.append(tableau(entetes, [28*mm, 16*mm, LU-64*mm, 20*mm], alt=True))
        story.append(Spacer(1, 5*mm))

    # ── 6. CHRONOGRAMME ──────────────────────────────────────────────────────
    chrono = donnees.get('chronogramme')
    if chrono:
        story.append(PageBreak())
        story.append(BandeauSection("6. Chronogramme", couleur=C_VERT))
        story.append(Spacer(1, 3*mm))

        paires_chrono = [
            ("Durée totale",                     chrono.get('duree_totale', '—')),
            ("Boucler",                           "Oui" if chrono.get('boucler') else "Non"),
            ("Arrêt acquisition en fin de cycle", "Oui" if chrono.get('arreter_acq_fin') else "Non"),
        ]
        story.append(kv_bloc(paires_chrono, styles, LU))
        story.append(Spacer(1, 4*mm))

        # Étapes
        etapes = chrono.get('etapes', [])
        if etapes:
            story.append(Paragraph("Étapes :", styles['h2']))
            entetes = [["N°", "Nom étape", "Durée (s)", "États sorties"]]
            for i, etape in enumerate(etapes):
                etats = etape.get('etats_sorties', '')
                entetes.append([
                    str(i+1),
                    etape.get('nom', ''),
                    str(etape.get('duree', '')),
                    etats if isinstance(etats, str) else "; ".join(str(e) for e in etats)
                ])
            story.append(tableau(entetes, [12*mm, 50*mm, 22*mm, LU-84*mm]))
            story.append(Spacer(1, 4*mm))

        # Règles conditionnelles
        regles = chrono.get('regles', [])
        if regles:
            story.append(Paragraph("Règles conditionnelles :", styles['h2']))
            entetes = [["Voie mesure", "Condition", "Seuil", "Sortie pilotée", "Action"]]
            for r in regles:
                entetes.append([
                    r.get('voie_nom', ''),
                    r.get('operateur', ''),
                    str(r.get('seuil', '')),
                    r.get('sortie_nom', ''),
                    r.get('action', '')
                ])
            story.append(tableau(entetes, [40*mm, 18*mm, 20*mm, 40*mm, LU-118*mm]))

    story.append(Spacer(1, 5*mm))

    # ── 7. PÉRIPHÉRIQUES ─────────────────────────────────────────────────────
    periphs = donnees.get('peripheriques', [])
    if periphs:
        story.append(BandeauSection("7. Bibliothèque de capteurs utilisés",
                                    couleur=C_ORANGE))
        story.append(Spacer(1, 3*mm))
        entetes = [["Nom capteur", "Type", "Unité", "Min", "Max"]]
        for p in periphs:
            entetes.append([
                p.get('nom',''),
                p.get('type',''),
                p.get('unite',''),
                str(p.get('min','—')),
                str(p.get('max','—'))
            ])
        story.append(tableau(entetes,
            [45*mm, 35*mm, 20*mm, 20*mm, LU-120*mm]))

    # ── Graphique d'acquisition ──────────────────────────────────────────────
    chemin_graphique = donnees.get('chemin_graphique', '')
    if chemin_graphique and os.path.exists(chemin_graphique):
        from reportlab.platypus import Image as RLImage
        story.append(PageBreak())
        story.append(BandeauSection("Graphique d'acquisition", couleur=colors.HexColor("#2A6080")))
        story.append(Spacer(1, 4*mm))
        try:
            img_g = RLImage(chemin_graphique, width=LU, height=LU * 700/1400)
            story.append(img_g)
        except Exception as ex:
            story.append(Paragraph(f"Impossible d'intégrer le graphique : {ex}", styles['note']))

    # ── Construction ─────────────────────────────────────────────────────────
    doc.build(
        story,
        onFirstPage=lambda c, d: on_page(c, d, donnees),
        onLaterPages=lambda c, d: on_page(c, d, donnees)
    )
    return chemin_pdf


# ─── Point d'entrée ───────────────────────────────────────────────────────────
if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python generer_rapport.py <json> <pdf>", file=sys.stderr)
        sys.exit(1)
    chemin_json = sys.argv[1]
    chemin_pdf  = sys.argv[2]
    with open(chemin_json, encoding='utf-8-sig') as f:
        donnees = json.load(f)
    donnees['date_generation'] = datetime.now().strftime("%d/%m/%Y %H:%M:%S")
    generer(donnees, chemin_pdf)
    print("OK:" + chemin_pdf)
