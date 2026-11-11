namespace AmigoSecretoDigital.Application;

/// <summary>Static entertainment phrases. They are intentionally independent of participant data.</summary>
public static class GiftPhraseCatalog
{
    private static readonly string[] Phrases =
    [
        "Regalale algo tan útil que hasta tu tía pregunte dónde lo compraste.",
        "Con que no sea otra taza que diga ‘la mejor persona’, ya vamos bien.",
        "Un regalo bonito y dentro del presupuesto: milagro navideño confirmado.",
        "Si no sabés qué regalar, una semita nunca ha dejado mal a nadie.",
        "Que el regalo no cueste un ojo; todavía falta comprar los panes con chumpe.",
        "Regalá con cariño, chero; la factura no se enseña en la cena.",
        "Algo que use de verdad, no otro adorno que termine junto al nacimiento.",
        "El mejor regalo es pensar en la persona… y guardar el recibo por si acaso.",
        "No te vayás de boca con el presupuesto, que todavía viene el 31.",
        "Un detallito con cariño vale más que endeudarse hasta Semana Santa.",
        "Que el regalo sea sorpresa, no una deuda sorpresa en enero.",
        "No regalés una dieta; regalá algo rico y que cada quien resuelva.",
        "Si le gusta el café, ya tenés media misión hecha, vos.",
        "Regalá algo que no necesite instrucciones de tres páginas.",
        "La intención cuenta; el papel brillante también hace su luchita.",
        "Elegí algo con cariño y envolvelo antes de que te agarre la medianoche.",
        "Un regalo práctico: más útil que el ventilador en diciembre.",
        "Evitá regalar calcetines… a menos que sepás que sí los necesita.",
        "Que no se note que lo compraste corriendo en el último puesto.",
        "No todo regalo necesita moño; pero ayuda a disimular la bolsa de tienda.",
        "Acordate: el presupuesto es un límite, no un reto personal.",
        "Algo hecho por vos puede quedar chivo, si la manualidad coopera.",
        "Regalale algo que no tenga que cambiar por una talla diferente.",
        "No regalés un compromiso: ‘te debo el regalo’ no cuenta como regalo.",
        "Una sorpresa alegre, como encontrar pupusas cuando ya tenías hambre.",
        "Pensá en sus gustos, no en lo que a vos te gustaría quedarte.",
        "Si escogés comida, confirmá que llegue antes de que todos le entren.",
        "El regalo no tiene que ser caro; el cariño tampoco trae precio fijo.",
        "Que sea algo bonito, no otra cosa que haya que desempolvar cada domingo.",
        "No te compliqués, chero: buen detalle, buena envoltura y listo.",
        "Regalá algo que sobreviva a la cena, la pólvora y el viaje de regreso.",
        "Un regalo pensado con calma le gana a comprar cualquier cosa en la gasolinera.",
        "Si no sabés su talla, mejor no jugués a la lotería con la ropa.",
        "Que el regalo no haga más ruido que la pólvora de medianoche.",
        "Algo pequeño y bien pensado puede quedar más chivo que algo enorme.",
        "No entregués el regalo con la etiqueta del precio viendo a todos.",
        "Una tarjeta escrita a mano: tecnología antigua, efecto poderoso.",
        "Regalá algo que le saque una sonrisa, no una pregunta de ‘¿y esto qué es?’.",
        "Si vas a regalar una planta, que no sea para ponerle más responsabilidades.",
        "Un detalle para disfrutar, no otro aparato que nadie sabe cómo prender.",
        "Que el regalo combine con la persona, no necesariamente con el arbolito.",
        "No dejés la compra para el último día; hasta Santa hace lista.",
        "Un regalo local siempre tiene su encanto, y apoya a alguien de aquí.",
        "No regalés algo que empiece con ‘yo tenía esto guardado’.",
        "La envoltura puede ser sencilla; lo importante es que no venga vacía.",
        "Regalá con buena onda, que el intercambio es para pasarla alegre.",
        "Un buen libro sirve; solo asegurate de que sí le guste leer.",
        "Si regalás dulces, guardá algunos para el camino. Consejo de chero.",
        "Que sea sorpresa hasta el final, no hasta que aparezca el recibo.",
        "Un detalle sincero vale más que decir ‘se me olvidó, pero feliz Navidad’.",
        "Elegí algo con cariño y que el próximo año te toque alguien igual de buena onda."
    ];

    public static IReadOnlyList<string> All => Phrases;

    public static string Pick(Random? random = null) => Phrases[(random ?? Random.Shared).Next(Phrases.Length)];
}
