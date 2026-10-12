select Customer, Sum(Amount) as Total, PositiveCount(Amount) as Positive,
       Avg(Quantity) as AverageQuantity
from invoice.rows('*')
group by Customer
order by Customer
