select Id, Amount, JsonText(Meta) as Metadata,
       Sum(Amount) over (order by Id) as Total
from json.file('{fixture}')
where Id > 1
order by Id
