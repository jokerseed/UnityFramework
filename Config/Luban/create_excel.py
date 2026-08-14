# -*- coding: utf-8 -*-
"""Create Luban Excel sources for battle tables."""
import os

from openpyxl import Workbook

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "Datas", "battle")


def write_sheet(path, header, rows):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    wb = Workbook()
    ws = wb.active
    names, types, groups = zip(*header)
    # Luban：首行 A1 必须为 ##var，且同行是字段名（meta 行会被再读一遍作标题）
    ws.append(["##var"] + list(names))
    ws.append(["##type"] + list(types))
    ws.append(["##group"] + list(groups))
    for row in rows:
        ws.append([""] + list(row))
    wb.save(path)


def main():
    ability_header = [
        ("id", "string", "c"),
        ("type", "CfgAbilityType", "c"),
        ("cooldown", "float", "c"),
        ("damage", "float", "c"),
        ("speed", "float", "c"),
        ("radius", "float", "c"),
        ("lifetime", "float", "c"),
        ("range", "float", "c"),
        ("cue_tag", "string", "c"),
        ("cost_attribute", "string", "c"),
        ("cost_amount", "float", "c"),
        ("required_tags", "string", "c"),
        ("blocked_tags", "string", "c"),
        ("damage_type", "CfgDamageType", "c"),
        ("half_angle", "float", "c"),
        ("pierce_count", "int", "c"),
        ("explode_radius", "float", "c"),
        ("hit_effect_id", "string", "c"),
        ("channel_time", "float", "c"),
        ("cooldown_group", "string", "c"),
        ("asset_tags", "string", "c"),
        ("owned_tags", "string", "c"),
        ("cancel_tags", "string", "c"),
        ("recovery_time", "float", "c"),
        ("knockback", "float", "c"),
        ("combo_effect_id", "string", "c"),
    ]
    abilities = [
        ("Fireball", "Projectile", 2, 20, 8, 0.25, 3, 0, "Cue.Fireball.Cast", "", 0, "", "", "Physical", 0, 0, 0, "", 0, "", "Ability.Fireball,Ability.Projectile", "", "", 0, 0, ""),
        ("Slash", "Melee", 0, 14, 0, 0, 0.12, 2.2, "Cue.Slash.Cast", "", 0, "", "Ability.Melee.Active", "Physical", 70, 0, 0, "", 0.08, "", "Ability.Slash,Ability.Melee", "Ability.Melee.Active", "", 0.2, 1.1, "ComboWindow"),
        ("Slash2", "Melee", 0, 16, 0, 0, 0.12, 2.3, "Cue.Slash2.Cast", "", 0, "Ability.Melee.ComboWindow", "", "Physical", 80, 0, 0, "", 0.06, "", "Ability.Slash,Ability.Melee", "Ability.Melee.Active", "Ability.Melee.Active", 0.18, 1.4, "ComboWindow"),
        ("Slash3", "Melee", 0, 22, 0, 0, 0.14, 2.5, "Cue.Slash3.Cast", "", 0, "Ability.Melee.ComboWindow", "", "Physical", 90, 0, 0, "Knockdown", 0.1, "", "Ability.Slash,Ability.Melee", "Ability.Melee.Active,State.HyperArmor", "Ability.Melee.Active", 0.4, 2.2, ""),
        ("MobSlash", "Melee", 1.2, 10, 0, 0, 0.1, 2.0, "Cue.MobSlash.Cast", "", 0, "", "Ability.Melee.Active", "Physical", 55, 0, 0, "", 0.16, "", "Ability.Slash,Ability.Melee", "Ability.Melee.Active", "", 0.25, 0.6, ""),
        ("Dodge", "Dash", 0.75, 0, 0, 0, 0, 0, "Cue.Dodge.Cast", "", 0, "", "", "Physical", 0, 0, 0, "IFrame", 0, "", "Ability.Dodge", "Ability.Dodge.Active", "Ability.Melee.Active", 0.28, 3.2, ""),
        ("Shockwave", "AoeCircle", 4, 18, 0, 2.5, 0, 0, "Cue.Shockwave.Cast", "Mana", 20, "", "", "Magical", 0, 0, 0, "", 0, "", "Ability.Shockwave,Ability.Aoe", "", "", 0, 0, ""),
        ("Cleave", "AoeCone", 3, 22, 0, 0, 0, 3, "Cue.Cleave.Cast", "", 0, "", "", "Physical", 45, 0, 0, "", 0, "", "Ability.Cleave,Ability.Aoe", "", "", 0, 0, ""),
        ("PierceBolt", "PierceProjectile", 3, 12, 10, 0.2, 2.5, 0, "Cue.PierceBolt.Cast", "", 0, "", "", "Physical", 0, 2, 0, "", 0, "", "Ability.PierceBolt,Ability.Projectile", "", "", 0, 0, ""),
        ("BoomShot", "ExplodeProjectile", 4, 10, 7, 0.3, 3, 0, "Cue.BoomShot.Cast", "", 0, "", "", "Magical", 0, 0, 1.5, "", 0, "", "Ability.BoomShot,Ability.Projectile", "", "", 0, 0, ""),
    ]
    write_sheet(os.path.join(OUT, "ability.xlsx"), ability_header, abilities)

    effect_header = [
        ("id", "string", "c"),
        ("duration_type", "CfgEffectDurationType", "c"),
        ("duration", "float", "c"),
        ("stacking", "CfgEffectStackingType", "c"),
        ("mod_attribute", "string", "c"),
        ("mod_operation", "int", "c"),
        ("mod_magnitude", "float", "c"),
        ("granted_tags", "string", "c"),
        ("required_tags", "string", "c"),
        ("blocked_tags", "string", "c"),
        ("immunity_tags", "string", "c"),
        ("period", "float", "c"),
        ("max_stacks", "int", "c"),
        ("cost_attribute", "string", "c"),
        ("cost_amount", "float", "c"),
        ("execution_type", "CfgEffectExecutionType", "c"),
        ("execution_effect_id", "string", "c"),
        ("cue_apply", "string", "c"),
        ("cue_remove", "string", "c"),
        ("shield_value", "float", "c"),
    ]
    effects = [
        ("Stun", "Duration", 1.5, "None", "", 0, 0, "State.CrowdControl.Stunned,Effect.Debuff", "", "", "", 0, 0, "", 0, "None", "", "Cue.Stun", "Cue.Stun", 0),
        ("Poison", "Duration", 6, "StackCount", "", 0, 0, "Effect.Debuff", "", "", "", 2, 3, "", 0, "Damage", "", "Cue.Poison", "Cue.Poison", 0),
        ("ShieldBubble", "Duration", 8, "RefreshDuration", "", 0, 0, "Effect.Buff", "", "", "", 0, 0, "", 0, "None", "", "Cue.Shield", "Cue.Shield", 40),
        ("Vulnerable", "Duration", 5, "RefreshDuration", "IncomingDamageMultiplier", 1, 1.5, "Effect.Debuff", "", "", "", 0, 0, "", 0, "None", "", "Cue.Vulnerable", "Cue.Vulnerable", 0),
        ("ComboWindow", "Duration", 0.45, "RefreshDuration", "", 0, 0, "Ability.Melee.ComboWindow", "", "", "", 0, 0, "", 0, "None", "", "", "", 0),
        ("IFrame", "Duration", 0.28, "RefreshDuration", "", 0, 0, "Immunity.Damage,State.Dodging", "", "", "", 0, 0, "", 0, "None", "", "Cue.Dodge.Cast", "Cue.Dodge.Cast", 0),
        ("Knockdown", "Duration", 1.2, "None", "", 0, 0, "State.CrowdControl.KnockedDown,Effect.Debuff", "", "", "", 0, 0, "", 0, "None", "", "Cue.Knockdown", "Cue.Knockdown", 0),
    ]
    write_sheet(os.path.join(OUT, "effect.xlsx"), effect_header, effects)

    cue_header = [
        ("id", "string", "c"),
        ("duration", "float", "c"),
        ("scale", "float", "c"),
        ("color_r", "float", "c"),
        ("color_g", "float", "c"),
        ("color_b", "float", "c"),
    ]
    cues = [
        ("Cue.Fireball.Cast", 0.35, 0.45, 1, 0.35, 0.08),
        ("Cue.Slash.Cast", 0.2, 0.35, 0.9, 0.9, 0.95),
        ("Cue.Slash2.Cast", 0.22, 0.42, 1, 0.85, 0.35),
        ("Cue.Slash3.Cast", 0.28, 0.55, 1, 0.55, 0.15),
        ("Cue.MobSlash.Cast", 0.2, 0.35, 0.75, 0.75, 0.8),
        ("Cue.Shockwave.Cast", 0.4, 1.2, 0.4, 0.7, 1),
        ("Cue.Cleave.Cast", 0.25, 0.6, 1, 0.85, 0.2),
        ("Cue.PierceBolt.Cast", 0.3, 0.3, 0.6, 0.9, 1),
        ("Cue.BoomShot.Cast", 0.4, 0.5, 1, 0.45, 0.1),
        ("Cue.Stun", 1.5, 0.5, 1, 1, 0.2),
        ("Cue.Poison", 0.4, 0.4, 0.3, 0.85, 0.2),
        ("Cue.Shield", 0.4, 0.7, 0.3, 0.7, 1),
        ("Cue.Dodge.Cast", 0.28, 0.5, 0.55, 0.85, 1),
        ("Cue.Knockdown", 0.35, 0.45, 0.7, 0.45, 1),
    ]
    write_sheet(os.path.join(OUT, "cue.xlsx"), cue_header, cues)
    print("Wrote", OUT)


if __name__ == "__main__":
    main()
