using System;
using System.Linq;
using System.Threading.Tasks;
using ECommerce.Common.Commands;
using ECommerce.Common.Events;
using ECommerce.Common.Infrastructure.Messaging;
using ECommerce.Sales.Api.Model;
using ECommerce.Sales.Api.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ECommerce.Sales.Api.Consumers
{
    public class SubmitOrderCommandConsumer : IConsumer<SubmitOrderCommand>
    {
        private readonly IDataService _dataService;
        private readonly SalesContext _salesContext;
        private readonly ILogger<SubmitOrderCommandConsumer> _logger;

        public SubmitOrderCommandConsumer(IDataService dataService, SalesContext salesContext, ILogger<SubmitOrderCommandConsumer> logger)
        {
            _dataService = dataService;
            _salesContext = salesContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SubmitOrderCommand> context)
        {
            _logger.LogInformation($"正在处理顾客 '{context.Message.CustomerId}' 的订单");

            var customer = await _dataService.GetCustomerAsync(context.Message.CustomerId);
            if (customer == null)
            {
                // probably we want to log this
                _logger.LogWarning($"来自顾客 {context.Message.CustomerId} 的订单不正确，系统中不存在这个顾客");

                return;
            }

            var products = await _dataService.GetProductsAsync();
            var order = new Order() { CustomerId = context.Message.CustomerId, Status = OrderStatus.Submitted };

            double total = 0.0;
            foreach (var item in context.Message.Items)
            {
                var product = products.FirstOrDefault(t => t.ProductId == item.ProductId);
                if (product != null)
                {
                    total += item.Quantity * product.Price;
                    order.Items.Add(new OrderItem() { ProductId = item.ProductId, Quantity = item.Quantity, Name = product.Name, Price = product.Price });
                }
            }

            // Business rule
            if (total > 100)
            {
                total = total * .9; // 10% off
                _logger.LogInformation($"为顾客 {customer.CustomerId} 的订单使用折扣后，总金额为 {total}");
            }
            order.Total = total;

            _salesContext.Orders.Add(order);
            _salesContext.SaveChanges();

            _logger.LogInformation($"已创建顾客 {customer.CustomerId} 的新订单，订单号为 {order.OrderId}，总金额 {order.Total}");

            await context.Publish(new OrderSubmittedEvent() {
                CorrelationId = context.Message.CorrelationId,
                CustomerId = customer.CustomerId,
                OrderId = order.OrderId,
                Total = order.Total,
                Products = order.Items.Select(t => new SubmittedOrderItem() { ProductId = t.ProductId, Quantity = t.Quantity }).ToArray()
            });
        }
    }
}
