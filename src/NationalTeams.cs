namespace LeoLauncher;
static class NationalCompetitions
{
    static readonly HashSet<string> Europe=["474","459","452","477","450","448","478","481","449","464","482","580","164","466","475","465","162","479","578","471"];
    static readonly HashSet<string> SouthAmerica=["202","205","208","209","210","212","207","211"];
    static readonly HashSet<string> Asia=["628","469","4375","627","2917","4398","655","451","2570","658"];
    static readonly HashSet<string> NorthAmerica=["206","11678","2654","203","2659","660"];
    public static (string Id,string Name)[] For(Club club)
    {
        var choices=new List<(string,string)>{("fifa.world","世界杯")};if(Europe.Contains(club.Id))choices.AddRange([("uefa.euro","欧洲杯"),("uefa.nations","欧国联"),("fifa.worldq.uefa","世预赛 · 欧洲")]);else if(SouthAmerica.Contains(club.Id))choices.AddRange([("conmebol.america","美洲杯"),("fifa.worldq.conmebol","世预赛 · 南美")]);else if(Asia.Contains(club.Id))choices.AddRange([("afc.asian.cup","亚洲杯"),("fifa.worldq.afc","世预赛 · 亚洲")]);else if(NorthAmerica.Contains(club.Id))choices.AddRange([("concacaf.gold","中北美金杯赛"),("fifa.worldq.concacaf","世预赛 · 中北美")]);else if(club.Id=="2666")choices.Add(("fifa.worldq.ofc","世预赛 · 大洋洲"));else choices.AddRange([("caf.nations","非洲杯"),("fifa.worldq.caf","世预赛 · 非洲")]);return choices.ToArray();
    }
    public static void ValidateTable(Club club,string json)
    {
        if(!club.IsNational)return;using var doc=System.Text.Json.JsonDocument.Parse(json);string name=doc.RootElement.TryGetProperty("name",out var value)?value.GetString()??"":"";
        bool Has(string text)=>name.Contains(text,StringComparison.OrdinalIgnoreCase);
        bool valid=club.League switch{"fifa.world"=>Has("World Cup")&&!Has("Qualifying"),"uefa.euro"=>Has("European Championship"),"uefa.nations"=>Has("Nations League"),"conmebol.america"=>Has("Copa Am"),"afc.asian.cup"=>Has("Asian Cup"),"concacaf.gold"=>Has("Gold Cup"),"caf.nations"=>Has("Africa Cup of Nations"),_=>club.League.StartsWith("fifa.worldq.")&&Has("World Cup Qualifying")&&Has(club.League.Split('.')[^1])};
        if(!valid)throw new FormatException("积分榜来源与所选国家队赛事不匹配");
    }
    public static string Name(Club club)=>club.IsNational?For(club).FirstOrDefault(c=>c.Id==club.League).Name??"国家队赛事":club.LeagueName;
}
