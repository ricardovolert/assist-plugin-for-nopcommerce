﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Assist.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.Assist.Controllers
{
    public class PaymentAssistController : BasePaymentController
    {
        #region Fields

        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ILocalizationService _localizationService;
        private readonly AssistPaymentSettings _assistPaymentSettings;
        private readonly PaymentSettings _paymentSettings;

        #endregion

        #region Ctor

        public PaymentAssistController(ISettingService settingService, 
            IPaymentService paymentService, 
            IOrderService orderService, 
            IOrderProcessingService orderProcessingService, 
            IWebHelper webHelper, 
            IWorkContext workContext, 
            ILocalizationService localizationService,
            AssistPaymentSettings assistPaymentSettings,
            PaymentSettings paymentSettings)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._webHelper = webHelper;
            this._workContext = workContext;
            this._localizationService = localizationService;
            this._assistPaymentSettings = assistPaymentSettings;
            this._paymentSettings = paymentSettings;
        }

        #endregion

        #region Methods

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new ConfigurationModel
            {
                MerchantId = _assistPaymentSettings.MerchantId,
                GatewayUrl = _assistPaymentSettings.GatewayUrl,
                AuthorizeOnly = _assistPaymentSettings.AuthorizeOnly,
                TestMode = _assistPaymentSettings.TestMode,
                AdditionalFee = _assistPaymentSettings.AdditionalFee
            };

            return View("~/Plugins/Payments.Assist/Views/PaymentAssist/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //save settings
            _assistPaymentSettings.GatewayUrl = model.GatewayUrl;
            _assistPaymentSettings.MerchantId = model.MerchantId;
            _assistPaymentSettings.AuthorizeOnly = model.AuthorizeOnly;
            _assistPaymentSettings.TestMode = model.TestMode;
            _assistPaymentSettings.AdditionalFee = model.AdditionalFee;

            _settingService.SaveSetting(_assistPaymentSettings);

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.Assist/Views/PaymentAssist/PaymentInfo.cshtml");
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();

            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();

            return paymentInfo;
        }

        public ActionResult Fail(FormCollection form)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Assist") as AssistPaymentProcessor;

            if (processor == null
                || !processor.IsPaymentMethodActive(_paymentSettings)
                || !processor.PluginDescriptor.Installed)
                throw new NopException("Assist module cannot be loaded");

            var order = _orderService.GetOrderById(_webHelper.QueryString<int>("ordernumber"));
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            return RedirectToRoute("OrderDetails", new { orderId = order.Id });
        }

        [ValidateInput(false)]
        public ActionResult Return(FormCollection form)
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Assist") as AssistPaymentProcessor;

            if (processor == null 
                || !processor.IsPaymentMethodActive(_paymentSettings) 
                || !processor.PluginDescriptor.Installed)
                throw new NopException("Assist module cannot be loaded");

            var order = _orderService.GetOrderById(_webHelper.QueryString<int>("ordernumber"));
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            if (_assistPaymentSettings.AuthorizeOnly)
            {
                if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                {
                    _orderProcessingService.MarkAsAuthorized(order);
                }
            }
            else
            {
                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                {
                    _orderProcessingService.MarkOrderAsPaid(order);
                }
            }

            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }

        #endregion
    }
}