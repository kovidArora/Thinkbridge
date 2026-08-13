The original `Quote` entity was anemic because it only contained properties. Any part of the application could create or modify a Quote without following any domain rules. Validation and behavior were handled outside the entity.

The rich model puts the important rules inside `Quote` itself. `Quote.Create(author, text)` ensures that every quote has an author between 1 and 200 characters and text between 1 and 1000 characters. This means invalid quotes cannot be created through the normal domain API.

The model also makes `Text` immutable after creation. Instead of allowing any code to change the text, the entity controls what can happen to itself. Soft deletion is also handled through a `Delete()` method, which sets the deletion flag rather than physically removing the quote.

A specific bug the anemic model could have shipped is an endpoint accidentally doing `quote.Text = ""` or changing an existing quote's text. The compiler would allow it, and the invalid quote could reach the database. With the rich model, `Text` cannot be changed after creation, so that bug is prevented by the domain model itself.

Overall, the rich model makes invalid states harder to create and keeps important business rules close to the data they protect.