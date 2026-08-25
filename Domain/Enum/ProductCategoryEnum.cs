namespace Domain.Enum
{
    public enum ProductCategoryEnum
    {
        Sheet,          // Blachy (gorącowalcowane, zimnowalcowane, ocynkowane)
        Pipe,           // Rury (ze szwem, bezszwowe)
        Bar,            // Pręty (okrągłe, żebrowane, kwadratowe, płaskowniki/bednarki)
        Profile,        // Profile zamknięte (kwadratowe, prostokątne)
        Beam,           // Kształtowniki / Dwuteowniki (HEA, HEB, IPE, ceowniki, kątowniki)
        Wire,           // Druty i walcówka
        Mesh,           // Siatki zbrojeniowe i maty
        Fitting,        // Armatura i łączniki (kolana, kołnierze, redukcje)
        Other           // Inne / Akcesoria hutnicze 
    }
}
