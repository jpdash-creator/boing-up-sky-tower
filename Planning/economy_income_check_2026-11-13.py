# Final income check: decided economy, engaged free player (does the tasks). All inputs are ASSUMPTIONS, change here.
P = {
 '30': dict(daily_pts=110, weekly_gems=18, weekly_task_coins=3900, weekly_hero_roll_coins=500, weekly_heroes=0.5, weekly_pu=3,
            play_coins_day=700, campaign_weeks=3, main_gems=250*0.70, main_weeks=8, full_days=0.0),
 '60': dict(daily_pts=170, weekly_gems=42, weekly_task_coins=5300, weekly_hero_roll_coins=1000, weekly_heroes=1.0, weekly_pu=5,
            play_coins_day=1200, campaign_weeks=1.5, main_gems=250*0.85, main_weeks=5, full_days=1/3),
}
MILESTONE_GEMS = 45
def daily(pts, full):
    c = 0
    for need, coins in ((40,200),(80,400),(120,600),(160,800)):
        if pts >= need: c += coins
    return c, full  # coins/day, full-day fraction
for k,p in P.items():
    dc,full = daily(p['daily_pts'], p['full_days'])
    day_coins = dc + full*0.5*1000                     # 200-pt gift: 50% hero else 1000 coins
    day_heroes = full*0.5
    wk_coins = 7*day_coins + p['weekly_task_coins'] + p['weekly_hero_roll_coins'] + 7*p['play_coins_day']
    wk_heroes = 7*day_heroes + p['weekly_heroes']
    one_time = (MILESTONE_GEMS + p['main_gems'])
    print(f"=== {k} min/day ===")
    print(f" steady gems/week {p['weekly_gems']} ({p['weekly_gems']/7:.1f}/day); steady coins/week {wk_coins:,.0f}; free hero copies/week {wk_heroes:.1f}; free power-ups/week {p['weekly_pu']}")
    # cumulative gems by week
    cum=0; first100=None; marks={}
    for w in range(1,13):
        g = p['weekly_gems']*(0.6 if w==1 else 1.0)
        g += (MILESTONE_GEMS/ max(p['campaign_weeks'],1)) * (1 if w<=p['campaign_weeks'] else 0) if w<=int(p['campaign_weeks']+0.99) else 0
        if w <= p['main_weeks']: g += p['main_gems']/p['main_weeks']
        cum += g
        if first100 is None and cum>=100: first100 = w - (cum-100)/g
        marks[w]=cum
    print(f" cumulative gems: wk1 {marks[1]:.0f}, wk2 {marks[2]:.0f}, wk4 {marks[4]:.0f}, wk8 {marks[8]:.0f}, wk12 {marks[12]:.0f}")
    print(f" first 100 gems at about week {first100:.1f}")
    for name,cost in (('Iron chest 100',100),('Gold chest 300',300),('Bundle avg 364',364),('Region skip 500',500)):
        print(f"   steady: {name}: {cost/p['weekly_gems']:.1f} weeks of steady gems")
    # coins vs sinks
    pu_use = 7*1250   # one power-up a day, average price 1,250
    left = wk_coins - pu_use + p['weekly_pu']*1250
    print(f" coins/week {wk_coins:,.0f}; one power-up a day costs {pu_use:,}; free PU worth {p['weekly_pu']*1250:,}; left for cards {left:,.0f}")
    for name,c in (('Common 2,000',2000),('Uncommon 5,000',5000),('Rare 12,000',12000),('Elite 32,000',32000),('Legendary 80,000',80000)):
        print(f"   {name}: {c/left:.1f} weeks of spare coins")
print()
print('Subscriber gem inflow per day (gem value): Starter', round(100/7+10,1), 'Plus', round(250/7+20+100/7,1),'Standard', round(200/30+10,1),'Premium', round(300/30+20+900/30,1))
